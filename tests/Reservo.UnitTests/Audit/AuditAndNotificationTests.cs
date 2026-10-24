using System.Text.Json;
using Reservo.Application.Audit;
using Reservo.Application.Notifications;
using Reservo.Domain.Audit;
using Reservo.Domain.Bookings;
using Reservo.Domain.Notifications;
using static Reservo.UnitTests.Bookings.BookingTestData;

namespace Reservo.UnitTests.Audit;

public class AuditAndNotificationTests
{
    [Fact]
    public void Changes_keep_only_fields_that_really_changed()
    {
        var changes = AuditEntries.Changes(
            ("title", "Встреча", "Встреча"),
            ("attendees", 4, 6),
            ("startAt", Msk(25, 14), Msk(25, 14).ToUniversalTime())); // тот же момент в другой записи — не изменение

        Assert.Equal(["attendees"], changes.Keys);
    }

    [Fact]
    public void Details_are_json_with_camel_case_cyrillic_and_enum_names()
    {
        var entry = AuditEntries.ForBooking(7, AuditAction.BookingUpdated, 42,
            AuditEntries.Changes(("status", BookingStatus.Confirmed, BookingStatus.Pending), ("title", "Старая тема", "Новая тема")), Now);

        Assert.Equal(AuditEntityTypes.Booking, entry.EntityType);
        Assert.Equal(42, entry.EntityId);

        var json = JsonDocument.Parse(entry.Details!).RootElement;
        Assert.Equal("Pending", json.GetProperty("status").GetProperty("new").GetString());
        Assert.Equal("Confirmed", json.GetProperty("status").GetProperty("old").GetString());
        Assert.Contains("Новая тема", entry.Details); // кириллица не превратилась в \uXXXX
    }

    [Fact]
    public void System_actions_have_no_actor()
    {
        var entry = AuditEntries.ForBooking(null, AuditAction.BookingCompleted, 1, null, Now);

        Assert.Null(entry.ActorId);
        Assert.Null(entry.Details);
    }

    [Fact]
    public void Rejection_notification_names_resource_local_time_and_reason()
    {
        var booking = TomorrowAt14(requiresApproval: true);

        var notification = BookingNotifications.Rejected(booking, "Лаборатория робототехники", "Лаборатория на ремонте", Policy, Now);

        Assert.Equal(NotificationType.BookingRejected, notification.Type);
        Assert.Equal(booking.UserId, notification.UserId);
        Assert.Equal("Бронь отклонена", notification.Title);
        Assert.Equal("Лаборатория робототехники, 25 октября с 14:00 до 15:00. Причина: Лаборатория на ремонте", notification.Message);
    }

    [Fact]
    public void Approval_request_goes_to_admin_not_owner()
    {
        var booking = TomorrowAt14(requiresApproval: true);

        var notification = BookingNotifications.ApprovalRequested(booking, "Лаборатория VR", "Иван Петров", adminId: 1, Policy, Now);

        Assert.Equal(1, notification.UserId);
        Assert.StartsWith("Иван Петров: Лаборатория VR, 25 октября", notification.Message);
    }

    [Fact]
    public void Reminder_says_how_many_minutes_are_left()
    {
        var booking = TomorrowAt14();

        var notification = BookingNotifications.Reminder(booking, "Переговорная «Байкал»", Policy, booking.StartAt.AddMinutes(-25));

        Assert.EndsWith("Начало через 25 мин.", notification.Message);
    }
}
