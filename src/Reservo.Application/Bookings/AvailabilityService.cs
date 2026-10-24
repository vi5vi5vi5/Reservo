using Microsoft.EntityFrameworkCore;
using Reservo.Application.Auth;
using Reservo.Application.Catalog.Resources;
using Reservo.Application.Common;
using Reservo.Application.Common.Errors;
using Reservo.Application.Common.Paging;
using Reservo.Domain.Bookings;

namespace Reservo.Application.Bookings;

/// <summary>Занятое время в расписании ресурса.</summary>
/// <param name="StartAt">Начало (UTC).</param>
/// <param name="EndAt">Конец (UTC).</param>
/// <param name="Status">Статус брони.</param>
/// <param name="IsMine">Бронь текущего пользователя.</param>
/// <param name="BookingId">Id брони — только для своих броней и для администратора.</param>
/// <param name="Title">Тема — только для своих броней и для администратора (BR-17).</param>
/// <param name="OwnerName">Владелец — только для администратора и для своих броней (BR-17).</param>
public sealed record ScheduleItemDto(
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    BookingStatus Status,
    bool IsMine,
    int? BookingId,
    string? Title,
    string? OwnerName);

/// <summary>Расписание ресурса за окно времени.</summary>
/// <param name="ResourceId">Id ресурса.</param>
/// <param name="ResourceName">Название ресурса.</param>
/// <param name="From">Начало окна (UTC).</param>
/// <param name="To">Конец окна (UTC).</param>
/// <param name="TimeZone">Часовой пояс площадки, в нём заданы рабочие часы. Фронтенду — чтобы показать местное время.</param>
/// <param name="OpenTime">Начало рабочего дня ресурса.</param>
/// <param name="CloseTime">Конец рабочего дня ресурса.</param>
/// <param name="Items">Занятые интервалы по возрастанию времени.</param>
public sealed record ResourceScheduleDto(
    int ResourceId,
    string ResourceName,
    DateTimeOffset From,
    DateTimeOffset To,
    string TimeZone,
    TimeOnly OpenTime,
    TimeOnly CloseTime,
    IReadOnlyList<ScheduleItemDto> Items);

public interface IAvailabilityService
{
    Task<PagedResult<ResourceDto>> GetAvailableResourcesAsync(AvailableResourcesQuery query, CancellationToken cancellationToken);

    Task<ResourceScheduleDto> GetScheduleAsync(int resourceId, ScheduleQuery query, CancellationToken cancellationToken);
}

public sealed class AvailabilityService(IAppDbContext db, ICurrentUser currentUser, BookingPolicy policy) : IAvailabilityService
{
    public async Task<PagedResult<ResourceDto>> GetAvailableResourcesAsync(AvailableResourcesQuery query, CancellationToken cancellationToken)
    {
        var from = query.From!.Value.ToUniversalTime();
        var to = query.To!.Value.ToUniversalTime();

        // Все фильтры каталога плюс три условия «можно забронировать на весь интервал».
        var resources = ResourceQueries.ApplyFilters(db.Resources.AsNoTracking(), query, includeInactive: false);

        // 1. Ни одной активной брони, пересекающей интервал. В SQL это подзапрос NOT EXISTS.
        resources = resources.Where(r => !db.Bookings.Any(b =>
            b.ResourceId == r.Id
            && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed)
            && b.StartAt < to && from < b.EndAt));

        // 2. Интервал укладывается в лимит длительности ресурса (BR-05).
        var minutes = (int)Math.Ceiling((to - from).TotalMinutes);
        resources = resources.Where(r => (r.MaxDurationMinutes ?? policy.DefaultMaxDurationMinutes) >= minutes);

        // 3. Интервал в рабочих часах ресурса (BR-06). Проверяется, когда интервал — в пределах одного местного дня;
        //    многодневный интервал забронировать одной бронью всё равно нельзя, и для него важна только занятость.
        var localFrom = policy.ToLocal(from);
        var localTo = policy.ToLocal(to);
        if (localFrom.Date == localTo.Date)
        {
            var openNeeded = TimeOnly.FromTimeSpan(localFrom.TimeOfDay);
            var closeNeeded = TimeOnly.FromTimeSpan(localTo.TimeOfDay);
            resources = resources.Where(r => r.OpenTime <= openNeeded && r.CloseTime >= closeNeeded);
        }

        return await ResourceQueries.ApplySorting(resources, query)
            .Select(ResourceMappings.ToDto)
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<ResourceScheduleDto> GetScheduleAsync(int resourceId, ScheduleQuery query, CancellationToken cancellationToken)
    {
        var from = query.From!.Value.ToUniversalTime();
        var to = query.To!.Value.ToUniversalTime();

        var resource = await db.Resources
            .AsNoTracking()
            .Where(r => r.Id == resourceId)
            .Select(r => new { r.Id, r.Name, r.OpenTime, r.CloseTime })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw CommonErrors.NotFound("Ресурс не найден.");

        // Занятое время — активные брони и уже прошедшие (Completed): в прошлом они тоже занимали ресурс.
        var bookings = await db.Bookings
            .AsNoTracking()
            .Where(b => b.ResourceId == resourceId
                        && (b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed || b.Status == BookingStatus.Completed)
                        && b.StartAt < to && from < b.EndAt)
            .OrderBy(b => b.StartAt)
            .Select(b => new { b.Id, b.StartAt, b.EndAt, b.Status, b.UserId, b.Title, OwnerName = b.User.FullName })
            .ToListAsync(cancellationToken);

        // Расписание открыто и для гостей, поэтому текущего пользователя может не быть.
        int? userId = currentUser.IsAuthenticated ? currentUser.Id : null;
        var isAdmin = currentUser.IsAdmin;

        // BR-17: чужие брони видны как занятое время — без темы, владельца и id. Подробности видят владелец и администратор.
        var items = bookings
            .Select(b =>
            {
                var isMine = b.UserId == userId;
                var showDetails = isMine || isAdmin;
                return new ScheduleItemDto(
                    b.StartAt,
                    b.EndAt,
                    b.Status,
                    isMine,
                    showDetails ? b.Id : null,
                    showDetails ? b.Title : null,
                    showDetails ? b.OwnerName : null);
            })
            .ToList();

        return new ResourceScheduleDto(
            resource.Id, resource.Name, from, to, policy.TimeZone.Id, resource.OpenTime, resource.CloseTime, items);
    }
}
