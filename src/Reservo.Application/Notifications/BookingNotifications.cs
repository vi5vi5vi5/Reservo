using Reservo.Domain.Bookings;
using Reservo.Domain.Notifications;

namespace Reservo.Application.Notifications;

/// <summary>
/// Тексты уведомлений о бронях (PROJECT.md, 5.5). Время пишется по местному времени площадки,
/// например «Переговорная «Байкал», 25 октября с 14:00 до 15:00».
/// </summary>
public static class BookingNotifications
{
    public static Notification Confirmed(Booking booking, string resourceName, BookingPolicy policy, DateTimeOffset now) =>
        ToOwner(booking, NotificationType.BookingConfirmed, "Бронь подтверждена", $"{Describe(booking, resourceName, policy)}.", now);

    public static Notification PendingApproval(Booking booking, string resourceName, BookingPolicy policy, DateTimeOffset now) =>
        ToOwner(booking, NotificationType.BookingPendingApproval, "Бронь ждёт согласования",
            $"{Describe(booking, resourceName, policy)}. Сообщим, когда администратор примет решение.", now);

    public static Notification ApprovalRequested(
        Booking booking, string resourceName, string requesterName, int adminId, BookingPolicy policy, DateTimeOffset now) =>
        new(adminId, booking.Id, NotificationType.ApprovalRequested, "Новая заявка на согласование",
            $"{requesterName}: {Describe(booking, resourceName, policy)}.", now);

    public static Notification Approved(Booking booking, string resourceName, BookingPolicy policy, DateTimeOffset now) =>
        ToOwner(booking, NotificationType.BookingApproved, "Бронь одобрена", $"{Describe(booking, resourceName, policy)}.", now);

    public static Notification Rejected(Booking booking, string resourceName, string reason, BookingPolicy policy, DateTimeOffset now) =>
        ToOwner(booking, NotificationType.BookingRejected, "Бронь отклонена",
            $"{Describe(booking, resourceName, policy)}. Причина: {reason}", now);

    public static Notification Cancelled(Booking booking, string resourceName, string reason, BookingPolicy policy, DateTimeOffset now) =>
        ToOwner(booking, NotificationType.BookingCancelled, "Бронь отменена",
            $"{Describe(booking, resourceName, policy)}. Причина: {reason}", now);

    public static Notification Reminder(Booking booking, string resourceName, BookingPolicy policy, DateTimeOffset now)
    {
        var minutes = (int)Math.Ceiling((booking.StartAt - now).TotalMinutes);
        return ToOwner(booking, NotificationType.BookingReminder, "Скоро начало брони",
            $"{Describe(booking, resourceName, policy)}. Начало через {minutes} мин.", now);
    }

    private static Notification ToOwner(Booking booking, NotificationType type, string title, string message, DateTimeOffset now) =>
        new(booking.UserId, booking.Id, type, title, message, now);

    private static string Describe(Booking booking, string resourceName, BookingPolicy policy) =>
        $"{resourceName}, {policy.DescribeInterval(booking.StartAt, booking.EndAt)}";
}
