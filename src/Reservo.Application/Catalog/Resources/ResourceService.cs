using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Reservo.Application.Auth;
using Reservo.Application.Common;
using Reservo.Application.Common.Errors;
using Reservo.Application.Common.Paging;
using Reservo.Domain.Catalog;

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
    TimeProvider timeProvider,
    ILogger<ResourceService> logger) : IResourceService
{
    public async Task<PagedResult<ResourceDto>> GetListAsync(ResourceListQuery query, CancellationToken cancellationToken)
    {
        // Запрос собирается по частям: каждый фильтр добавляет условие WHERE, только если он задан.
        // Выполняется он один раз — в ToPagedResultAsync.
        var resources = db.Resources.AsNoTracking();

        // Выведенные из эксплуатации ресурсы видит только администратор, и только если попросил.
        if (!(query.IncludeInactive && currentUser.IsAdmin))
        {
            resources = resources.Where(r => r.IsActive);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            resources = resources.Where(r =>
                r.Name.ToLower().Contains(term)
                || (r.Description != null && r.Description.ToLower().Contains(term)));
        }

        if (query.CategoryId is { } categoryId)
        {
            resources = resources.Where(r => r.CategoryId == categoryId);
        }

        if (query.LocationId is { } locationId)
        {
            resources = resources.Where(r => r.LocationId == locationId);
        }

        if (query.MinCapacity is { } minCapacity)
        {
            resources = resources.Where(r => r.Capacity >= minCapacity);
        }

        var featureIds = query.FeatureIds.Distinct().ToArray();
        if (featureIds.Length > 0)
        {
            // «Есть всё перечисленное»: среди оснащения ресурса нашлись все запрошенные id.
            resources = resources.Where(r => r.Features.Count(f => featureIds.Contains(f.Id)) == featureIds.Length);
        }

        var sorted = (query.SortBy, query.SortDir) switch
        {
            // Ресурсы без вместимости (оборудование) всегда в конце списка.
            (ResourceSortBy.Capacity, SortDirection.Asc) =>
                resources.OrderBy(r => r.Capacity == null).ThenBy(r => r.Capacity).ThenBy(r => r.Name),
            (ResourceSortBy.Capacity, SortDirection.Desc) =>
                resources.OrderBy(r => r.Capacity == null).ThenByDescending(r => r.Capacity).ThenBy(r => r.Name),
            (_, SortDirection.Desc) => resources.OrderByDescending(r => r.Name),
            _ => resources.OrderBy(r => r.Name),
        };

        // Id в конце сортировки делает порядок однозначным, иначе при пагинации записи могли бы «прыгать» между страницами.
        return await sorted
            .ThenBy(r => r.Id)
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
        await EnsureCategoryAndLocationExistAsync(request, cancellationToken);
        var features = await LoadFeaturesAsync(request.FeatureIds, cancellationToken);

        var resource = new Resource(request.ToDetails(), timeProvider.GetUtcNow());
        resource.SetFeatures(features);

        db.Resources.Add(resource);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Создан ресурс {ResourceId} «{Name}»", resource.Id, resource.Name);
        return await GetByIdAsync(resource.Id, cancellationToken);
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

        resource.Update(request.ToDetails(), timeProvider.GetUtcNow());
        resource.SetFeatures(features);
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

        resource.Deactivate(timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        // На этапе бронирований здесь же будут отменяться будущие брони ресурса (BR-15).
        logger.LogInformation("Ресурс {ResourceId} выведен из эксплуатации. Причина: {Reason}", id, request.Reason);
    }

    public async Task ActivateAsync(int id, CancellationToken cancellationToken)
    {
        var resource = await FindAsync(id, cancellationToken);
        if (resource.IsActive)
        {
            return;
        }

        resource.Activate(timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Ресурс {ResourceId} возвращён в эксплуатацию", id);
    }

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
