using FluentValidation;
using Reservo.Application.Catalog.Resources;
using Reservo.Application.Common;
using Reservo.Application.Common.Paging;
using Reservo.Domain.Bookings;

namespace Reservo.Application.Bookings;

/// <summary>Фильтры списка броней.</summary>
public class BookingListQuery : PagedQuery
{
    /// <summary>Только брони в этом статусе.</summary>
    public BookingStatus? Status { get; init; }

    /// <summary>Только брони, которые заканчиваются позже этого момента. Например, <c>from</c> = сейчас — предстоящие и идущие.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Только брони, которые начинаются раньше этого момента.</summary>
    public DateTimeOffset? To { get; init; }

    /// <summary>Сортировка по началу брони: <c>Asc</c> или <c>Desc</c>.</summary>
    public SortDirection SortDir { get; init; } = SortDirection.Asc;
}

/// <summary>Фильтры списка всех броней для администратора.</summary>
public sealed class AdminBookingListQuery : BookingListQuery
{
    /// <summary>Только брони этого ресурса.</summary>
    public int? ResourceId { get; init; }

    /// <summary>Только брони этого пользователя.</summary>
    public int? UserId { get; init; }
}

/// <summary>Поиск ресурсов, свободных на весь интервал, плюс все фильтры каталога.</summary>
public sealed class AvailableResourcesQuery : ResourceListQuery, ITimeWindowQuery
{
    /// <summary>Начало интервала, со смещением.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Конец интервала, со смещением.</summary>
    public DateTimeOffset? To { get; init; }
}

/// <summary>Окно расписания ресурса.</summary>
public sealed record ScheduleQuery : ITimeWindowQuery
{
    /// <summary>Начало окна, со смещением.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Конец окна, со смещением. Окно не длиннее 31 дня.</summary>
    public DateTimeOffset? To { get; init; }
}

public sealed class BookingListQueryValidator : AbstractValidator<BookingListQuery>
{
    public BookingListQueryValidator()
    {
        Include(new PagedQueryValidator());

        RuleFor(x => x.To)
            .Must((query, to) => to > query.From).WithMessage("Конец интервала должен быть позже начала.")
            .When(x => x.From is not null && x.To is not null);
    }
}

public sealed class AdminBookingListQueryValidator : AbstractValidator<AdminBookingListQuery>
{
    public AdminBookingListQueryValidator()
    {
        Include(new BookingListQueryValidator());

        RuleFor(x => x.ResourceId).GreaterThan(0).WithMessage("Id ресурса должен быть положительным.");
        RuleFor(x => x.UserId).GreaterThan(0).WithMessage("Id пользователя должен быть положительным.");
    }
}

public sealed class AvailableResourcesQueryValidator : AbstractValidator<AvailableResourcesQuery>
{
    public AvailableResourcesQueryValidator()
    {
        Include(new ResourceListQueryValidator());
        Include(new TimeWindowValidator());
    }
}

public sealed class ScheduleQueryValidator : AbstractValidator<ScheduleQuery>
{
    public ScheduleQueryValidator() => Include(new TimeWindowValidator());
}
