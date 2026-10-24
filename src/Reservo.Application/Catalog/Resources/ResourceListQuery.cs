using FluentValidation;
using Reservo.Application.Common.Paging;

namespace Reservo.Application.Catalog.Resources;

/// <summary>Поле сортировки каталога.</summary>
public enum ResourceSortBy
{
    Name,
    Capacity,
}

/// <summary>Фильтры каталога ресурсов.</summary>
public class ResourceListQuery : PagedQuery
{
    public const int SearchMaxLength = 100;
    public const int MaxFeatureFilters = 20;

    /// <summary>Поиск по названию и описанию, без учёта регистра.</summary>
    public string? Search { get; init; }

    /// <summary>Только ресурсы этой категории.</summary>
    public int? CategoryId { get; init; }

    /// <summary>Только ресурсы этого корпуса.</summary>
    public int? LocationId { get; init; }

    /// <summary>Минимальная вместимость. Ресурсы без вместимости (оборудование) при этом не попадают в выдачу.</summary>
    public int? MinCapacity { get; init; }

    /// <summary>Нужное оснащение: у ресурса должно быть всё перечисленное. Передаётся так: <c>featureIds=1&amp;featureIds=2</c>.</summary>
    public int[] FeatureIds { get; init; } = [];

    /// <summary>Сортировка: <c>Name</c> или <c>Capacity</c>.</summary>
    public ResourceSortBy SortBy { get; init; } = ResourceSortBy.Name;

    /// <summary>Направление сортировки: <c>Asc</c> или <c>Desc</c>.</summary>
    public SortDirection SortDir { get; init; } = SortDirection.Asc;

    /// <summary>Показать и выведенные из эксплуатации ресурсы. Работает только для администратора.</summary>
    public bool IncludeInactive { get; init; }
}

public sealed class ResourceListQueryValidator : AbstractValidator<ResourceListQuery>
{
    public ResourceListQueryValidator()
    {
        Include(new PagedQueryValidator());

        RuleFor(x => x.Search)
            .MaximumLength(ResourceListQuery.SearchMaxLength)
            .WithMessage($"Строка поиска не длиннее {ResourceListQuery.SearchMaxLength} символов.");

        RuleFor(x => x.CategoryId).GreaterThan(0).WithMessage("Id категории должен быть положительным.");
        RuleFor(x => x.LocationId).GreaterThan(0).WithMessage("Id корпуса должен быть положительным.");
        RuleFor(x => x.MinCapacity).GreaterThan(0).WithMessage("Минимальная вместимость должна быть больше нуля.");

        RuleFor(x => x.FeatureIds)
            .Must(ids => ids.Length <= ResourceListQuery.MaxFeatureFilters)
            .WithMessage($"Не больше {ResourceListQuery.MaxFeatureFilters} фильтров по оснащению.");

        RuleForEach(x => x.FeatureIds).GreaterThan(0).WithMessage("Id оснащения должен быть положительным.");
    }
}
