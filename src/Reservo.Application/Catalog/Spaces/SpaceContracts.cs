using FluentValidation;
using Reservo.Application.Catalog.Locations;
using Reservo.Domain.Catalog;

namespace Reservo.Application.Catalog.Spaces;

/// <summary>Состояние места на запрошенный интервал.</summary>
public enum SeatStatus
{
    /// <summary>Свободно на весь интервал — можно бронировать.</summary>
    Free,

    /// <summary>Занято чужой бронью.</summary>
    Busy,

    /// <summary>На это время у текущего пользователя уже есть бронь этого места.</summary>
    Mine,

    /// <summary>Забронировать нельзя по другой причине: место выведено из эксплуатации, интервал вне часов работы или длиннее лимита.</summary>
    Unavailable,
}

/// <summary>Место на схеме коворкинга.</summary>
/// <param name="ResourceId">Id ресурса — по нему бронируют (<c>POST /api/bookings</c>) и смотрят расписание.</param>
/// <param name="Number">Номер места.</param>
/// <param name="Row">Ряд на схеме, с 1.</param>
/// <param name="Column">Колонка на схеме, с 1.</param>
/// <param name="FeatureCodes">Коды оснащения места, например <c>computers</c>, <c>window</c>.</param>
/// <param name="IsActive"><c>false</c> — место выведено из эксплуатации.</param>
/// <param name="Status">Состояние на интервал <c>from</c>–<c>to</c>. Пусто, если интервал не задан.</param>
/// <param name="FreeUntil">У свободного места — до какого момента оно свободно после конца интервала.</param>
public sealed record SeatDto(
    int ResourceId,
    int Number,
    int Row,
    int Column,
    IReadOnlyList<string> FeatureCodes,
    bool IsActive,
    SeatStatus? Status,
    DateTimeOffset? FreeUntil);

/// <summary>Коворкинг со схемой мест.</summary>
/// <param name="Id">Идентификатор.</param>
/// <param name="Name">Название.</param>
/// <param name="Description">Описание.</param>
/// <param name="Location">Корпус.</param>
/// <param name="Floor">Этаж.</param>
/// <param name="IsQuiet">Тихая зона.</param>
/// <param name="Rows">Рядов на схеме.</param>
/// <param name="Columns">Мест в ряду на схеме.</param>
/// <param name="AisleAfterColumn">После какой колонки проход; пусто — прохода нет.</param>
/// <param name="OpenTime">Открывается — по самому раннему месту. Пусто, если мест нет.</param>
/// <param name="CloseTime">Закрывается — по самому позднему месту. Пусто, если мест нет.</param>
/// <param name="TotalSeats">Сколько мест в эксплуатации.</param>
/// <param name="FreeSeats">Сколько из них свободно на интервал. Пусто, если интервал не задан.</param>
/// <param name="Seats">Все места по номерам.</param>
public sealed record SpaceDto(
    int Id,
    string Name,
    string? Description,
    LocationDto Location,
    int? Floor,
    bool IsQuiet,
    int Rows,
    int Columns,
    int? AisleAfterColumn,
    TimeOnly? OpenTime,
    TimeOnly? CloseTime,
    int TotalSeats,
    int? FreeSeats,
    IReadOnlyList<SeatDto> Seats);

/// <summary>Интервал, на который показать свободные места. Необязателен: без него схема приходит без состояний.</summary>
public sealed record SpaceAvailabilityQuery
{
    /// <summary>Начало интервала, со смещением.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Конец интервала, со смещением.</summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>Только коворкинги этого корпуса.</summary>
    public int? LocationId { get; init; }
}

public sealed class SpaceAvailabilityQueryValidator : AbstractValidator<SpaceAvailabilityQuery>
{
    public const int MaxDays = 1;

    public SpaceAvailabilityQueryValidator()
    {
        RuleFor(x => x)
            .Must(x => (x.From is null) == (x.To is null))
            .WithName("from")
            .WithMessage("Укажите оба края интервала (from и to) или ни одного.");

        RuleFor(x => x.To)
            .Must((query, to) => to > query.From).WithMessage("Конец интервала должен быть позже начала.")
            .Must((query, to) => to - query.From <= TimeSpan.FromDays(MaxDays)).WithMessage("Интервал не длиннее суток.")
            .When(x => x.From is not null && x.To is not null);

        RuleFor(x => x.LocationId).GreaterThan(0).WithMessage("Id корпуса должен быть положительным.");
    }
}

/// <summary>Данные коворкинга для создания или изменения. Места добавляются как ресурсы категории вида <c>Seat</c>.</summary>
public sealed record SpaceRequest
{
    /// <summary>Id корпуса.</summary>
    /// <example>1</example>
    public int LocationId { get; init; }

    /// <summary>Название.</summary>
    /// <example>Коворкинг «Точка»</example>
    public string Name { get; init; } = string.Empty;

    /// <summary>Описание, необязательно.</summary>
    public string? Description { get; init; }

    /// <summary>Этаж, необязательно.</summary>
    /// <example>1</example>
    public int? Floor { get; init; }

    /// <summary>Тихая зона: без звонков и разговоров.</summary>
    public bool IsQuiet { get; init; }

    /// <summary>Рядов на схеме.</summary>
    /// <example>4</example>
    public int Rows { get; init; }

    /// <summary>Мест в ряду на схеме.</summary>
    /// <example>6</example>
    public int Columns { get; init; }

    /// <summary>После какой колонки проход, необязательно.</summary>
    /// <example>3</example>
    public int? AisleAfterColumn { get; init; }

    public SpaceDetails ToDetails() => new(LocationId, Name, Description, Floor, IsQuiet, Rows, Columns, AisleAfterColumn);
}

public sealed class SpaceRequestValidator : AbstractValidator<SpaceRequest>
{
    public SpaceRequestValidator()
    {
        RuleFor(x => x.LocationId).GreaterThan(0).WithMessage("Укажите корпус.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Укажите название.")
            .MaximumLength(Space.NameMaxLength).WithMessage($"Название не длиннее {Space.NameMaxLength} символов.");

        RuleFor(x => x.Description)
            .MaximumLength(Space.DescriptionMaxLength).WithMessage($"Описание не длиннее {Space.DescriptionMaxLength} символов.");

        RuleFor(x => x.Floor).InclusiveBetween(-5, 100).WithMessage("Этаж должен быть от -5 до 100.");
        RuleFor(x => x.Rows).InclusiveBetween(1, Space.MaxRows).WithMessage($"Рядов — от 1 до {Space.MaxRows}.");
        RuleFor(x => x.Columns).InclusiveBetween(1, Space.MaxColumns).WithMessage($"Мест в ряду — от 1 до {Space.MaxColumns}.");

        RuleFor(x => x.AisleAfterColumn)
            .Must((request, aisle) => aisle >= 1 && aisle < request.Columns)
            .WithMessage("Проход должен быть между колонками схемы: от 1 до числа колонок минус один.")
            .When(x => x.AisleAfterColumn is not null);
    }
}
