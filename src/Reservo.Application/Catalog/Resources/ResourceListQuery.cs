using FluentValidation;
using Reservo.Application.Common.Paging;
using Reservo.Domain.Catalog;

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
    public const int MaxIdFilters = 50;

    /// <summary>Поиск по названию и описанию, без учёта регистра.</summary>
    public string? Search { get; init; }

    /// <summary>Только ресурсы этих категорий: <c>categoryIds=1&amp;categoryIds=2</c>.</summary>
    public int[] CategoryIds { get; init; } = [];

    /// <summary>Только ресурсы этих корпусов: <c>locationIds=1&amp;locationIds=3</c>.</summary>
    public int[] LocationIds { get; init; } = [];

    /// <summary>
    /// Только ресурсы этих видов: <c>Room</c>, <c>Seat</c>, <c>Equipment</c>. Если вид не указан, места в коворкингах
    /// в выдачу не попадают — их много, и показываются они на схеме зала (<c>GET /api/spaces</c>).
    /// </summary>
    public ResourceKind[] Kinds { get; init; } = [];

    /// <summary>Только места этого коворкинга.</summary>
    public int? SpaceId { get; init; }

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

        RuleFor(x => x.CategoryIds)
            .Must(ids => ids.Length <= ResourceListQuery.MaxIdFilters)
            .WithMessage($"Не больше {ResourceListQuery.MaxIdFilters} категорий в фильтре.");
        RuleForEach(x => x.CategoryIds).GreaterThan(0).WithMessage("Id категории должен быть положительным.");

        RuleFor(x => x.LocationIds)
            .Must(ids => ids.Length <= ResourceListQuery.MaxIdFilters)
            .WithMessage($"Не больше {ResourceListQuery.MaxIdFilters} корпусов в фильтре.");
        RuleForEach(x => x.LocationIds).GreaterThan(0).WithMessage("Id корпуса должен быть положительным.");

        RuleForEach(x => x.Kinds).IsInEnum().WithMessage("Вид ресурса — Room, Seat или Equipment.");
        RuleFor(x => x.SpaceId).GreaterThan(0).WithMessage("Id коворкинга должен быть положительным.");
        RuleFor(x => x.MinCapacity).GreaterThan(0).WithMessage("Минимальная вместимость должна быть больше нуля.");

        RuleFor(x => x.FeatureIds)
            .Must(ids => ids.Length <= ResourceListQuery.MaxFeatureFilters)
            .WithMessage($"Не больше {ResourceListQuery.MaxFeatureFilters} фильтров по оснащению.");

        RuleForEach(x => x.FeatureIds).GreaterThan(0).WithMessage("Id оснащения должен быть положительным.");
    }
}
