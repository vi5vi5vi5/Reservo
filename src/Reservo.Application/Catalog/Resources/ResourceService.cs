using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Reservo.Application.Audit;
using Reservo.Application.Auth;
using Reservo.Application.Common;
using Reservo.Application.Common.Errors;
using Reservo.Application.Common.Paging;
using Reservo.Application.Notifications;
using Reservo.Application.Realtime;
using Reservo.Domain.Audit;
using Reservo.Domain.Bookings;
using Reservo.Domain.Catalog;
using Reservo.Domain.Notifications;

namespace Reservo.Application.Catalog.Resources;

public interface IResourceService
{
    Task<PagedResult<ResourceDto>> GetListAsync(ResourceListQuery query, CancellationToken cancellationToken);

    Task<ResourceDto> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<ResourceDto> CreateAsync(ResourceRequest request, CancellationToken cancellationToken);

    Task<ResourceDto> UpdateAsync(int id, ResourceRequest request, CancellationToken cancellationToken);

    Task DeactivateAsync(int id, DeactivateResourceRequest request, CancellationToken cancellationToken);

    Task ActivateAsync(int id, CancellationToken cancellationToken);
}

public sealed class ResourceService(
    IAppDbContext db,
    ICurrentUser currentUser,
    BookingPolicy policy,
    TimeProvider timeProvider,
    IRealtimeNotifier realtime,
    ILogger<ResourceService> logger) : IResourceService
{
    public async Task<PagedResult<ResourceDto>> GetListAsync(ResourceListQuery query, CancellationToken cancellationToken)
    {
        // Выведенные из эксплуатации ресурсы видит только администратор, и только если попросил.
        var includeInactive = query.IncludeInactive && currentUser.IsAdmin;
        var resources = ResourceQueries.ApplyFilters(db.Resources.AsNoTracking(), query, includeInactive);

        return await ResourceQueries.ApplySorting(resources, query)
            .Select(ResourceMappings.ToDto)
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<ResourceDto> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        await db.Resources
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Select(ResourceMappings.ToDto)
            .SingleOrDefaultAsync(cancellationToken)
        ?? throw NotFound();

    public async Task<ResourceDto> CreateAsync(ResourceRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        // Ресурс и запись журнала — одной транзакцией: журналу нужен id ресурса, который выдаёт база при сохранении.
        var resourceId = await db.ExecuteInTransactionAsync(async ct =>
        {
            await EnsureCategoryAndLocationExistAsync(request, ct);
            var features = await LoadFeaturesAsync(request.FeatureIds, ct);

            var resource = new Resource(request.ToDetails(), now);
            resource.SetFeatures(features);
            db.Resources.Add(resource);
            await db.SaveChangesAsync(ct);

            db.AuditLogs.Add(AuditEntries.ForResource(currentUser.Id, AuditAction.ResourceCreated, resource.Id, new { resource.Name }, now));
            await db.SaveChangesAsync(ct);

            return resource.Id;
        }, cancellationToken);

        logger.LogInformation("Создан ресурс {ResourceId}", resourceId);
        return await GetByIdAsync(resourceId, cancellationToken);
    }

    public async Task<ResourceDto> UpdateAsync(int id, ResourceRequest request, CancellationToken cancellationToken)
    {
        // Include нужен, чтобы EF Core знал текущее оснащение и смог вычислить, какие связи добавить, а какие удалить.
        var resource = await db.Resources
            .Include(r => r.Features)
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken)
            ?? throw NotFound();

        await EnsureCategoryAndLocationExistAsync(request, cancellationToken);
        var features = await LoadFeaturesAsync(request.FeatureIds, cancellationToken);

        var before = Snapshot(resource);
        resource.Update(request.ToDetails(), timeProvider.GetUtcNow());
        resource.SetFeatures(features);
        var after = Snapshot(resource);

        db.AuditLogs.Add(AuditEntries.ForResource(currentUser.Id, AuditAction.ResourceUpdated, id, AuditEntries.Changes(
            ("name", before.Name, after.Name),
            ("categoryId", before.CategoryId, after.CategoryId),
            ("locationId", before.LocationId, after.LocationId),
            ("floor", before.Floor, after.Floor),
            ("capacity", before.Capacity, after.Capacity),
            ("requiresApproval", before.RequiresApproval, after.RequiresApproval),
            ("openTime", before.OpenTime, after.OpenTime),
            ("closeTime", before.CloseTime, after.CloseTime),
            ("maxDurationMinutes", before.MaxDurationMinutes, after.MaxDurationMinutes),
            ("imageUrl", before.ImageUrl, after.ImageUrl),
            ("description", before.Description, after.Description),
            ("features", before.Features, after.Features)), timeProvider.GetUtcNow()));

        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Изменён ресурс {ResourceId}", id);
        return await GetByIdAsync(id, cancellationToken);
    }

    public async Task DeactivateAsync(int id, DeactivateResourceRequest request, CancellationToken cancellationToken)
    {
        var resource = await FindAsync(id, cancellationToken);
        if (!resource.IsActive)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var actorId = currentUser.Id;
        var reason = $"Ресурс выведен из эксплуатации: {request.Reason.Trim()}";

        resource.Deactivate(now);

        // BR-15: все активные брони, которые ещё не закончились, отменяются; владельцы получают уведомления.
        var affected = await db.Bookings
            .Where(b => b.ResourceId == id
                        && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed)
                        && b.EndAt > now)
            .ToListAsync(cancellationToken);

        var notifications = new List<Notification>();
        foreach (var booking in affected)
        {
            booking.Cancel(reason, now);
            db.AuditLogs.Add(AuditEntries.ForBooking(actorId, AuditAction.BookingCancelled, booking.Id, new { reason }, now));
            notifications.Add(BookingNotifications.Cancelled(booking, resource.Name, reason, policy, now));
        }

        db.AuditLogs.Add(AuditEntries.ForResource(actorId, AuditAction.ResourceDeactivated, id,
            new { reason = request.Reason.Trim(), cancelledBookings = affected.Count }, now));
        db.Notifications.AddRange(notifications);

        // Ресурс, его брони, журнал и уведомления сохраняются одной транзакцией: либо всё, либо ничего.
        await db.SaveChangesAsync(cancellationToken);

        await realtime.NotificationsCreatedAsync(notifications, cancellationToken);
        await realtime.ScheduleChangedAsync([id], cancellationToken);

        logger.LogInformation(
            "Ресурс {ResourceId} выведен из эксплуатации, отменено броней: {CancelledBookings}. Причина: {Reason}",
            id, affected.Count, request.Reason);
    }

    public async Task ActivateAsync(int id, CancellationToken cancellationToken)
    {
        var resource = await FindAsync(id, cancellationToken);
        if (resource.IsActive)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        resource.Activate(now);
        db.AuditLogs.Add(AuditEntries.ForResource(currentUser.Id, AuditAction.ResourceActivated, id, null, now));
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Ресурс {ResourceId} возвращён в эксплуатацию", id);
    }

    /// <summary>Снимок полей для журнала: что было до изменения и что стало после.</summary>
    private static ResourceSnapshot Snapshot(Resource r) => new(
        r.Name, r.CategoryId, r.LocationId, r.Floor, r.Capacity, r.RequiresApproval, r.OpenTime, r.CloseTime,
        r.MaxDurationMinutes, r.ImageUrl, r.Description,
        // Список оснащения сравнивается строкой «1,2,5»: массивы сравнивались бы по ссылке, а не по содержимому.
        string.Join(",", r.Features.Select(f => f.Id).Order()));

    private sealed record ResourceSnapshot(
        string Name, int CategoryId, int LocationId, int? Floor, int? Capacity, bool RequiresApproval, TimeOnly OpenTime,
        TimeOnly CloseTime, int? MaxDurationMinutes, string? ImageUrl, string? Description, string Features);

    private async Task<Resource> FindAsync(int id, CancellationToken cancellationToken) =>
        await db.Resources.FindAsync([id], cancellationToken) ?? throw NotFound();

    private async Task EnsureCategoryAndLocationExistAsync(ResourceRequest request, CancellationToken cancellationToken)
    {
        if (!await db.Categories.AnyAsync(c => c.Id == request.CategoryId, cancellationToken))
        {
            throw CommonErrors.ReferenceNotFound($"Категория с id {request.CategoryId} не найдена.");
        }

        if (!await db.Locations.AnyAsync(l => l.Id == request.LocationId, cancellationToken))
        {
            throw CommonErrors.ReferenceNotFound($"Корпус с id {request.LocationId} не найден.");
        }
    }

    private async Task<List<Feature>> LoadFeaturesAsync(IEnumerable<int> featureIds, CancellationToken cancellationToken)
    {
        var ids = featureIds.Distinct().ToArray();
        var features = await db.Features.Where(f => ids.Contains(f.Id)).ToListAsync(cancellationToken);

        var missing = ids.Except(features.Select(f => f.Id)).ToArray();
        if (missing.Length > 0)
        {
            throw CommonErrors.ReferenceNotFound($"Оснащение не найдено: id {string.Join(", ", missing)}.");
        }

        return features;
    }

    private static AppException NotFound() => CommonErrors.NotFound("Ресурс не найден.");
}
