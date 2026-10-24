using Microsoft.EntityFrameworkCore;
using Reservo.Application.Audit;
using Reservo.Application.Common;
using Reservo.Application.Notifications;
using Reservo.Application.Realtime;
using Reservo.Domain.Audit;
using Reservo.Domain.Bookings;
using Reservo.Domain.Notifications;

namespace Reservo.Application.Bookings;

/// <summary>Сколько броней обработала фоновая задача за один проход.</summary>
public sealed record BookingLifecycleResult(int Rejected, int Completed, int Reminded)
{
    public int Total => Rejected + Completed + Reminded;
}

public interface IBookingLifecycleService
{
    Task<BookingLifecycleResult> RunAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Работа фоновой задачи (PROJECT.md, 8.4): автоотклонение несогласованных заявок, автозавершение прошедших броней,
/// напоминания перед началом. Действует от имени системы: в журнале у таких записей нет автора.
/// </summary>
/// <remarks>
/// SQL-запрос отбирает кандидатов, а окончательное решение принимает чистая функция из BookingLifecycle —
/// та же, что покрыта unit-тестами. За один проход обрабатывается не больше BatchSize броней каждого вида,
/// остальные дождутся следующего прохода через минуту.
/// </remarks>
public sealed class BookingLifecycleService(
    IAppDbContext db,
    BookingPolicy policy,
    TimeProvider timeProvider,
    IRealtimeNotifier realtime) : IBookingLifecycleService
{
    private const int BatchSize = 100;

    public async Task<BookingLifecycleResult> RunAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        var rejected = await AutoRejectAsync(now, cancellationToken);
        var completed = await CompleteAsync(now, cancellationToken);
        var reminded = await RemindAsync(now, cancellationToken);

        return new BookingLifecycleResult(rejected, completed, reminded);
    }

    /// <summary>UC-20: заявки, которые так и не согласовали до начала, отклоняются с уведомлением владельцу.</summary>
    private async Task<int> AutoRejectAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var bookings = (await LoadAsync(b => b.Status == BookingStatus.Pending && b.StartAt <= now, cancellationToken))
            .Where(b => BookingLifecycle.ShouldAutoReject(b, now))
            .ToList();

        var notifications = new List<Notification>();
        foreach (var booking in bookings)
        {
            booking.Reject(BookingLifecycle.AutoRejectReason, now);
            db.AuditLogs.Add(AuditEntries.ForBooking(null, AuditAction.BookingRejected, booking.Id, new { reason = BookingLifecycle.AutoRejectReason }, now));
            notifications.Add(BookingNotifications.Rejected(booking, booking.Resource.Name, BookingLifecycle.AutoRejectReason, policy, now));
        }

        await SaveAndPublishAsync(bookings, notifications, cancellationToken);
        return bookings.Count;
    }

    /// <summary>UC-19: подтверждённые брони, время которых прошло, становятся завершёнными.</summary>
    private async Task<int> CompleteAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var bookings = (await LoadAsync(b => b.Status == BookingStatus.Confirmed && b.EndAt <= now, cancellationToken))
            .Where(b => BookingLifecycle.ShouldComplete(b, now))
            .ToList();

        foreach (var booking in bookings)
        {
            booking.Complete(now);
            db.AuditLogs.Add(AuditEntries.ForBooking(null, AuditAction.BookingCompleted, booking.Id, null, now));
        }

        await SaveAndPublishAsync(bookings, [], cancellationToken);
        return bookings.Count;
    }

    /// <summary>UC-18: напоминание за ReminderMinutesBefore минут до начала, один раз на бронь.</summary>
    private async Task<int> RemindAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var remindUntil = now.AddMinutes(policy.ReminderMinutesBefore);

        var bookings = (await LoadAsync(
                b => b.Status == BookingStatus.Confirmed && b.ReminderSentAt == null && b.StartAt > now && b.StartAt <= remindUntil,
                cancellationToken))
            .Where(b => BookingLifecycle.ShouldRemind(b, now, policy))
            .ToList();

        if (bookings.Count == 0)
        {
            return 0;
        }

        var notifications = new List<Notification>();
        foreach (var booking in bookings)
        {
            booking.MarkReminderSent(now);
            notifications.Add(BookingNotifications.Reminder(booking, booking.Resource.Name, policy, now));
        }

        // Напоминание не меняет расписание, поэтому scheduleChanged здесь не нужен.
        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync(cancellationToken);
        await realtime.NotificationsCreatedAsync(notifications, cancellationToken);

        return bookings.Count;
    }

    private Task<List<Booking>> LoadAsync(System.Linq.Expressions.Expression<Func<Booking, bool>> filter, CancellationToken cancellationToken) =>
        db.Bookings
            .Include(b => b.Resource)
            .Where(filter)
            .OrderBy(b => b.StartAt)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

    private async Task SaveAndPublishAsync(List<Booking> bookings, List<Notification> notifications, CancellationToken cancellationToken)
    {
        if (bookings.Count == 0)
        {
            return;
        }

        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync(cancellationToken);

        await realtime.NotificationsCreatedAsync(notifications, cancellationToken);
        await realtime.ScheduleChangedAsync(bookings.Select(b => b.ResourceId).Distinct(), cancellationToken);
    }
}
