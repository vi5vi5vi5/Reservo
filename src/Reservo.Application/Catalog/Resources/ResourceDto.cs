using System.Linq.Expressions;
using Reservo.Application.Catalog.Categories;
using Reservo.Application.Catalog.Features;
using Reservo.Application.Catalog.Locations;
using Reservo.Domain.Catalog;

namespace Reservo.Application.Catalog.Resources;

/// <summary>Ресурс: помещение или единица оборудования.</summary>
/// <param name="Id">Идентификатор.</param>
/// <param name="Name">Название.</param>
/// <param name="Description">Описание.</param>
/// <param name="Category">Категория.</param>
/// <param name="Location">Корпус.</param>
/// <param name="Floor">Этаж.</param>
/// <param name="Capacity">Вместимость в людях; у оборудования не задаётся.</param>
/// <param name="ImageUrl">Ссылка на фото.</param>
/// <param name="RequiresApproval">Брони подтверждает администратор.</param>
/// <param name="OpenTime">Начало рабочего дня, местное время площадки.</param>
/// <param name="CloseTime">Конец рабочего дня, местное время площадки.</param>
/// <param name="MaxDurationMinutes">Максимальная длительность одной брони в минутах; пусто — общий лимит.</param>
/// <param name="IsActive"><c>false</c> — ресурс выведен из эксплуатации.</param>
/// <param name="Features">Оснащение.</param>
/// <param name="CreatedAt">Когда создан (UTC).</param>
/// <param name="UpdatedAt">Когда последний раз изменён (UTC).</param>
public sealed record ResourceDto(
    int Id,
    string Name,
    string? Description,
    CategoryDto Category,
    LocationDto Location,
    int? Floor,
    int? Capacity,
    string? ImageUrl,
    bool RequiresApproval,
    TimeOnly OpenTime,
    TimeOnly CloseTime,
    int? MaxDurationMinutes,
    bool IsActive,
    IReadOnlyList<FeatureDto> Features,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public static class ResourceMappings
{
    /// <summary>
    /// Проекция для LINQ-запросов: EF Core превращает её в один SQL-запрос, который выбирает
    /// только нужные колонки вместе с категорией, корпусом и оснащением — без отдельных Include.
    /// </summary>
    public static readonly Expression<Func<Resource, ResourceDto>> ToDto = r => new ResourceDto(
        r.Id,
        r.Name,
        r.Description,
        new CategoryDto(r.Category.Id, r.Category.Code, r.Category.Name, r.Category.Description),
        new LocationDto(r.Location.Id, r.Location.Name, r.Location.Address),
        r.Floor,
        r.Capacity,
        r.ImageUrl,
        r.RequiresApproval,
        r.OpenTime,
        r.CloseTime,
        r.MaxDurationMinutes,
        r.IsActive,
        r.Features.OrderBy(f => f.Name).Select(f => new FeatureDto(f.Id, f.Code, f.Name)).ToList(),
        r.CreatedAt,
        r.UpdatedAt);
}
