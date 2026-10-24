using Reservo.Domain.Common;

namespace Reservo.Domain.Bookings;

/// <summary>
/// Допустимые переходы между статусами брони (PROJECT.md, 5.4). Всё, чего нет в списке, запрещено.
/// Rejected, Cancelled и Completed — конечные статусы: из них переходов нет.
/// </summary>
public static class BookingStateMachine
{
    private static readonly HashSet<(BookingStatus From, BookingStatus To)> AllowedTransitions =
    [
        (BookingStatus.Pending, BookingStatus.Confirmed),   // админ одобрил
        (BookingStatus.Pending, BookingStatus.Rejected),    // админ отклонил или не успели согласовать
        (BookingStatus.Pending, BookingStatus.Cancelled),   // отмена
        (BookingStatus.Confirmed, BookingStatus.Pending),   // перенос времени брони ресурса с согласованием (BR-14)
        (BookingStatus.Confirmed, BookingStatus.Cancelled), // отмена
        (BookingStatus.Confirmed, BookingStatus.Completed), // время прошло
    ];

    public static bool CanTransition(BookingStatus from, BookingStatus to) => AllowedTransitions.Contains((from, to));

    public static DomainError? CheckTransition(BookingStatus from, BookingStatus to) =>
        CanTransition(from, to)
            ? null
            : new DomainError(
                "booking.invalid_status_transition",
                $"Бронь в статусе «{from.ToRussian()}» нельзя перевести в статус «{to.ToRussian()}».",
                IsConflict: true);
}
