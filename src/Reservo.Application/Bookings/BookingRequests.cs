using FluentValidation;
using Reservo.Application.Catalog.Resources;
using Reservo.Domain.Bookings;

namespace Reservo.Application.Bookings;

/// <summary>Общие поля создания и изменения брони.</summary>
public interface IBookingDetails
{
    DateTimeOffset? StartAt { get; }

    DateTimeOffset? EndAt { get; }

    string Title { get; }

    int? Attendees { get; }
}

/// <summary>Новая бронь.</summary>
public sealed record CreateBookingRequest : IBookingDetails
{
    /// <summary>Id ресурса.</summary>
    /// <example>1</example>
    public int ResourceId { get; init; }

    /// <summary>Начало: дата и время обязательно со смещением, например +03:00 для Москвы.</summary>
    /// <example>2026-10-26T14:00:00+03:00</example>
    public DateTimeOffset? StartAt { get; init; }

    /// <summary>Конец (не включительно), тоже со смещением.</summary>
    /// <example>2026-10-26T15:30:00+03:00</example>
    public DateTimeOffset? EndAt { get; init; }

    /// <summary>Тема или цель.</summary>
    /// <example>Встреча проектной команды</example>
    public string Title { get; init; } = string.Empty;

    /// <summary>Число участников, необязательно.</summary>
    /// <example>6</example>
    public int? Attendees { get; init; }
}

/// <summary>Изменение брони: время, тема, участники. Ресурс сменить нельзя — для этого отмена и новая бронь.</summary>
public sealed record UpdateBookingRequest : IBookingDetails
{
    /// <summary>Новое начало, со смещением.</summary>
    /// <example>2026-10-26T15:00:00+03:00</example>
    public DateTimeOffset? StartAt { get; init; }

    /// <summary>Новый конец, со смещением.</summary>
    /// <example>2026-10-26T16:30:00+03:00</example>
    public DateTimeOffset? EndAt { get; init; }

    /// <summary>Тема или цель.</summary>
    /// <example>Встреча проектной команды</example>
    public string Title { get; init; } = string.Empty;

    /// <summary>Число участников, необязательно.</summary>
    /// <example>6</example>
    public int? Attendees { get; init; }
}

/// <summary>Отмена брони.</summary>
public sealed record CancelBookingRequest
{
    /// <summary>Причина. Владельцу необязательна, администратору при отмене чужой брони обязательна.</summary>
    /// <example>Встречу перенесли</example>
    public string? Reason { get; init; }
}

/// <summary>Отклонение заявки администратором.</summary>
public sealed record RejectBookingRequest
{
    /// <summary>Причина, её увидит владелец брони.</summary>
    /// <example>Лаборатория в этот день занята под практикум</example>
    public string Reason { get; init; } = string.Empty;
}

public sealed class BookingDetailsValidator : AbstractValidator<IBookingDetails>
{
    public BookingDetailsValidator()
    {
        RuleFor(x => x.StartAt).NotNull().WithMessage("Укажите начало брони.");
        RuleFor(x => x.EndAt).NotNull().WithMessage("Укажите конец брони.");

        // BR-01. Остальные правила времени (сетка, рабочие часы и т. д.) проверяет доменная логика: им нужны ресурс и настройки.
        RuleFor(x => x.EndAt)
            .Must((request, end) => end > request.StartAt).WithMessage("Конец брони должен быть позже начала.")
            .When(x => x.StartAt is not null && x.EndAt is not null);

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Укажите тему брони.")
            .MaximumLength(Booking.TitleMaxLength).WithMessage($"Тема не длиннее {Booking.TitleMaxLength} символов.");

        RuleFor(x => x.Attendees)
            .InclusiveBetween(1, ResourceRequest.MaxCapacity)
            .WithMessage($"Число участников — от 1 до {ResourceRequest.MaxCapacity}.");
    }
}

public sealed class CreateBookingRequestValidator : AbstractValidator<CreateBookingRequest>
{
    public CreateBookingRequestValidator()
    {
        RuleFor(x => x.ResourceId).GreaterThan(0).WithMessage("Укажите ресурс.");
        Include(new BookingDetailsValidator());
    }
}

public sealed class UpdateBookingRequestValidator : AbstractValidator<UpdateBookingRequest>
{
    public UpdateBookingRequestValidator() => Include(new BookingDetailsValidator());
}

public sealed class CancelBookingRequestValidator : AbstractValidator<CancelBookingRequest>
{
    public CancelBookingRequestValidator()
    {
        RuleFor(x => x.Reason)
            .MaximumLength(Booking.StatusReasonMaxLength)
            .WithMessage($"Причина не длиннее {Booking.StatusReasonMaxLength} символов.");
    }
}

public sealed class RejectBookingRequestValidator : AbstractValidator<RejectBookingRequest>
{
    public RejectBookingRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Укажите причину отклонения.")
            .MaximumLength(Booking.StatusReasonMaxLength)
            .WithMessage($"Причина не длиннее {Booking.StatusReasonMaxLength} символов.");
    }
}
