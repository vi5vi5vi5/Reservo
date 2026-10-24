namespace Reservo.Domain.Bookings;

/// <summary>
/// Что фоновая задача должна сделать с бронью в момент <c>now</c> (PROJECT.md, 8.4).
/// Это чистые функции: их проверяют unit-тесты с любым «текущим временем», а SQL-запросы задачи
/// повторяют те же условия, чтобы не загружать из базы лишние брони.
/// </summary>
public static class BookingLifecycle
{
    public const string AutoRejectReason = "Заявку не успели согласовать до начала брони.";

    /// <summary>UC-20: заявка так и не согласована, а время брони уже наступило — отклоняем.</summary>
    public static bool ShouldAutoReject(Booking booking, DateTimeOffset now) =>
        booking.Status == BookingStatus.Pending && booking.StartAt <= now;

    /// <summary>UC-19: время подтверждённой брони прошло — завершаем.</summary>
    public static bool ShouldComplete(Booking booking, DateTimeOffset now) =>
        booking.Status == BookingStatus.Confirmed && booking.EndAt <= now;

    /// <summary>
    /// UC-18: до начала подтверждённой брони осталось не больше ReminderMinutesBefore минут,
    /// а напоминания ещё не было.
    /// </summary>
    public static bool ShouldRemind(Booking booking, DateTimeOffset now, BookingPolicy policy) =>
        booking.Status == BookingStatus.Confirmed
        && booking.ReminderSentAt is null
        && booking.StartAt > now
        && booking.StartAt <= now.AddMinutes(policy.ReminderMinutesBefore);
}
