using Reservo.Application.Common.Paging;
using Reservo.Domain.Catalog;

namespace Reservo.Application.Catalog.Resources;

/// <summary>
/// Фильтры и сортировка каталога. Ими пользуются и сам каталог, и поиск свободных ресурсов,
/// поэтому они вынесены в одно место.
/// </summary>
internal static class ResourceQueries
{
    /// <summary>
    /// Запрос собирается по частям: каждый фильтр добавляет условие WHERE, только если он задан.
    /// Выполняется он один раз — там, где его материализуют (ToListAsync, CountAsync).
    /// </summary>
    public static IQueryable<Resource> ApplyFilters(IQueryable<Resource> resources, ResourceListQuery query, bool includeInactive)
    {
        if (!includeInactive)
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

        return resources;
    }

    public static IOrderedQueryable<Resource> ApplySorting(IQueryable<Resource> resources, ResourceListQuery query)
    {
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
        return sorted.ThenBy(r => r.Id);
    }
}
