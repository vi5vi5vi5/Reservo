namespace Reservo.Application.Common.Paging;

/// <summary>Базовый класс для запросов списков с пагинацией.</summary>
public abstract class PagedQuery
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;

    /// <summary>Номер страницы, начиная с 1.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Размер страницы, от 1 до 100.</summary>
    public int PageSize { get; init; } = DefaultPageSize;
}

/// <summary>Направление сортировки.</summary>
public enum SortDirection
{
    Asc,
    Desc,
}
