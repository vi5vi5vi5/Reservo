using Microsoft.EntityFrameworkCore;

namespace Reservo.Application.Common.Paging;

/// <summary>Одна страница списка.</summary>
/// <param name="Items">Элементы текущей страницы.</param>
/// <param name="Page">Номер страницы, начиная с 1.</param>
/// <param name="PageSize">Размер страницы.</param>
/// <param name="TotalCount">Сколько всего элементов во всём списке.</param>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

public static class PagingExtensions
{
    /// <summary>Выполняет два SQL-запроса: COUNT для общего числа и OFFSET/LIMIT для нужной страницы.</summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, PagedQuery paging, CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((paging.Page - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, paging.Page, paging.PageSize, totalCount);
    }
}
