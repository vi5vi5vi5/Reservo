using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Reservo.Application.Audit;
using Reservo.Application.Auth;
using Reservo.Application.Common;
using Reservo.Application.Common.Errors;
using Reservo.Application.Common.Paging;
using Reservo.Application.Notifications;
using Reservo.Application.Realtime;
using Reservo.Domain.Audit;
using Reservo.Domain.Bookings;
using Reservo.Domain.Catalog;
using Reservo.Domain.Notifications;
using Reservo.Domain.Users;

namespace Reservo.Application.Bookings;

public interface IBookingService
{
    Task<BookingDto> CreateAsync(CreateBookingRequest request, CancellationToken cancellationToken);

    Task<BookingDto> UpdateAsync(int id, UpdateBookingRequest request, CancellationToken cancellationToken);

    Task<BookingDto> CancelAsync(int id, CancelBookingRequest request, CancellationToken cancellationToken);

    Task<BookingDto> GetByIdAsync(int id, CancellationToken cancellationToken);

    Task<PagedResult<BookingDto>> GetMyAsync(BookingListQuery query, CancellationToken cancellationToken);

    Task<PagedResult<BookingDto>> GetAllAsync(AdminBookingListQuery query, CancellationToken cancellationToken);

    Task<BookingDto> ApproveAsync(int id, CancellationToken cancellationToken);

    Task<BookingDto> RejectAsync(int id, RejectBookingRequest request, CancellationToken cancellationToken);
}

/// <remarks>
/// Каждое изменение брони сохраняется вместе с записью в журнале операций и уведомлениями — одной транзакцией
/// (PROJECT.md, 5.5 и 5.6). Живые обновления через SignalR уходят только после сохранения.
/// Уведомление не отправляется тому, кто сам совершил действие: он и так о нём знает.
/// </remarks>
public sealed class BookingService(
    IAppDbContext db,
    ICurrentUser currentUser,
    BookingPolicy policy,
    TimeProvider timeProvider,
    IRealtimeNotifier realtime,
    ILogger<BookingService> logger) : IBookingService
{
    public async Task<BookingDto> CreateAsync(CreateBookingRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var userId = currentUser.Id;

        // Npgsql записывает в timestamptz только UTC, поэтому всё входящее время сразу переводим в UTC.
        var startAt = request.StartAt!.Value.ToUniversalTime();
        var endAt = request.EndAt!.Value.ToUniversalTime();

        // Бронь, журнал и уведомления — одной транзакцией. Журналу и уведомлениям нужен id брони, а его выдаёт база
        // только при сохранении, поэтому сохранений два: сначала бронь, затем всё остальное.
        var (booking, notifications) = await db.ExecuteInTransactionAsync(async ct =>
        {
            var resource = await db.Resources.AsNoTracking().SingleOrDefaultAsync(r => r.Id == request.ResourceId, ct)
                ?? throw BookingErrors.ResourceNotFound(request.ResourceId);

            // 1. Правила, которым не нужна база (BR-02–BR-08).
            BookingRules.CheckSlot(startAt, endAt, request.Attendees, resource, policy, now).ThrowIfViolated();

            // 2. Правила, которым нужны другие брони (BR-09–BR-11).
            await EnsureNoOverlapsAsync(resource, userId, startAt, endAt, exceptBookingId: 0, ct);

            var activeBookings = await ActiveBookings().CountAsync(b => b.UserId == userId && b.StartAt > now, ct);
            BookingRules.CheckUserLimit(activeBookings, policy).ThrowIfViolated();

            var booking = new Booking(resource.Id, userId, startAt, endAt, request.Title, request.Attendees, resource.RequiresApproval, now);
            db.Bookings.Add(booking);

            // 3. Если параллельный запрос успел занять то же время между проверкой и сохранением,
            //    exclusion constraint в базе не даст записать пересечение, и клиент получит 409 (ConstraintErrors).
            await db.SaveChangesAsync(ct);

            db.AuditLogs.Add(AuditEntries.ForBooking(userId, AuditAction.BookingCreated, booking.Id, new
            {
                resourceId = resource.Id,
                resource = resource.Name,
                booking.StartAt,
                booking.EndAt,
                booking.Title,
                booking.Attendees,
                booking.Status,
            }, now));

            var notifications = booking.Status == BookingStatus.Pending
                ? await PendingApprovalNotificationsAsync(booking, resource.Name, now, ct)
                : new List<Notification> { BookingNotifications.Confirmed(booking, resource.Name, policy, now) };

            db.Notifications.AddRange(notifications);
            await db.SaveChangesAsync(ct);

            return (booking, notifications);
        }, cancellationToken);

        await PublishAsync(notifications, booking.ResourceId, cancellationToken);

        logger.LogInformation(
            "Пользователь {UserId} забронировал ресурс {ResourceId}: бронь {BookingId}, статус {Status}",
            userId, booking.ResourceId, booking.Id, booking.Status);

        return await LoadAsync(booking.Id, cancellationToken);
    }

    public async Task<BookingDto> UpdateAsync(int id, UpdateBookingRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var startAt = request.StartAt!.Value.ToUniversalTime();
        var endAt = request.EndAt!.Value.ToUniversalTime();

        var booking = await db.Bookings
            .Include(b => b.Resource)
            .SingleOrDefaultAsync(b => b.Id == id, cancellationToken);

        EnsureCanSee(booking);

        // BR-12: менять бронь может только владелец. Администратор её видит, но может только отменить.
        if (booking.UserId != currentUser.Id)
        {
            throw BookingErrors.OnlyOwnerCanModify();
        }

        BookingRules.CheckCanModify(booking, now).ThrowIfViolated();
        BookingRules.CheckSlot(startAt, endAt, request.Attendees, booking.Resource, policy, now).ThrowIfViolated();

        // Сама бронь из проверки пересечений исключается: она не конфликтует сама с собой.
        // Лимит броней не проверяем — их число от изменения не меняется.
        await EnsureNoOverlapsAsync(booking.Resource, booking.UserId, startAt, endAt, exceptBookingId: booking.Id, cancellationToken);

        var before = new { booking.StartAt, booking.EndAt, booking.Title, booking.Attendees, booking.Status };
        booking.Update(startAt, endAt, request.Title, request.Attendees, booking.Resource.RequiresApproval, now);

        db.AuditLogs.Add(AuditEntries.ForBooking(currentUser.Id, AuditAction.BookingUpdated, booking.Id, AuditEntries.Changes(
            ("startAt", before.StartAt, booking.StartAt),
            ("endAt", before.EndAt, booking.EndAt),
            ("title", before.Title, booking.Title),
            ("attendees", before.Attendees, booking.Attendees),
            ("status", before.Status, booking.Status)), now));

        // Перенесённая бронь, которая ждёт согласования, снова попадает в очередь администраторов (PROJECT.md, 5.5).
        var timeChanged = before.StartAt != booking.StartAt || before.EndAt != booking.EndAt;
        var notifications = timeChanged && booking.Status == BookingStatus.Pending
            ? await PendingApprovalNotificationsAsync(booking, booking.Resource.Name, now, cancellationToken)
            : [];

        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync(cancellationToken);
        await PublishAsync(notifications, booking.ResourceId, cancellationToken);

        logger.LogInformation("Изменена бронь {BookingId}, статус {StatusBefore} → {StatusAfter}", id, before.Status, booking.Status);
        return await LoadAsync(id, cancellationToken);
    }

    public async Task<BookingDto> CancelAsync(int id, CancelBookingRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var booking = await db.Bookings.Include(b => b.Resource).SingleOrDefaultAsync(b => b.Id == id, cancellationToken);

        EnsureCanSee(booking);

        var isOwner = booking.UserId == currentUser.Id;
        BookingRules.CheckCanCancel(booking, byAdmin: currentUser.IsAdmin, now).ThrowIfViolated();

        // BR-13: отменяя чужую бронь, администратор обязан объяснить причину владельцу.
        if (!isOwner && string.IsNullOrWhiteSpace(request.Reason))
        {
            throw BookingErrors.CancelReasonRequired();
        }

        booking.Cancel(request.Reason, now);
        db.AuditLogs.Add(AuditEntries.ForBooking(currentUser.Id, AuditAction.BookingCancelled, booking.Id, new { reason = booking.StatusReason }, now));

        var notifications = isOwner
            ? []
            : new List<Notification> { BookingNotifications.Cancelled(booking, booking.Resource.Name, booking.StatusReason!, policy, now) };

        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync(cancellationToken);
        await PublishAsync(notifications, booking.ResourceId, cancellationToken);

        logger.LogInformation("Бронь {BookingId} отменена пользователем {UserId}", id, currentUser.Id);
        return await LoadAsync(id, cancellationToken);
    }

    public async Task<BookingDto> GetByIdAsync(int id, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var isAdmin = currentUser.IsAdmin;

        // Чужую бронь обычный пользователь не находит вовсе — получает 404, а не 403.
        return await db.Bookings
            .AsNoTracking()
            .Where(b => b.Id == id && (isAdmin || b.UserId == userId))
            .Select(BookingMappings.ToDto)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw BookingErrors.NotFound();
    }

    public async Task<PagedResult<BookingDto>> GetMyAsync(BookingListQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var bookings = ApplyFilters(db.Bookings.AsNoTracking().Where(b => b.UserId == userId), query);

        return await Sort(bookings, query.SortDir)
            .Select(BookingMappings.ToDto)
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<PagedResult<BookingDto>> GetAllAsync(AdminBookingListQuery query, CancellationToken cancellationToken)
    {
        var bookings = ApplyFilters(db.Bookings.AsNoTracking(), query);

        if (query.ResourceId is { } resourceId)
        {
            bookings = bookings.Where(b => b.ResourceId == resourceId);
        }

        if (query.UserId is { } userId)
        {
            bookings = bookings.Where(b => b.UserId == userId);
        }

        return await Sort(bookings, query.SortDir)
            .Select(BookingMappings.ToDto)
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<BookingDto> ApproveAsync(int id, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var booking = await db.Bookings.Include(b => b.Resource).SingleOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw BookingErrors.NotFound();

        // Заявка на согласовании уже занимает время в расписании (Pending — активный статус),
        // поэтому пересечений при одобрении быть не может и проверять их не нужно.
        BookingRules.CheckCanApprove(booking, now).ThrowIfViolated();

        booking.Approve(now);
        db.AuditLogs.Add(AuditEntries.ForBooking(currentUser.Id, AuditAction.BookingApproved, booking.Id, null, now));

        var notifications = NotifyOwnerUnlessActor(booking, () => BookingNotifications.Approved(booking, booking.Resource.Name, policy, now));
        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync(cancellationToken);
        await PublishAsync(notifications, booking.ResourceId, cancellationToken);

        logger.LogInformation("Администратор {AdminId} одобрил бронь {BookingId}", currentUser.Id, id);
        return await LoadAsync(id, cancellationToken);
    }

    public async Task<BookingDto> RejectAsync(int id, RejectBookingRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var booking = await db.Bookings.Include(b => b.Resource).SingleOrDefaultAsync(b => b.Id == id, cancellationToken)
            ?? throw BookingErrors.NotFound();

        BookingRules.CheckCanReject(booking).ThrowIfViolated();

        booking.Reject(request.Reason, now);
        db.AuditLogs.Add(AuditEntries.ForBooking(currentUser.Id, AuditAction.BookingRejected, booking.Id, new { reason = booking.StatusReason }, now));

        var notifications = NotifyOwnerUnlessActor(booking,
            () => BookingNotifications.Rejected(booking, booking.Resource.Name, booking.StatusReason!, policy, now));
        db.Notifications.AddRange(notifications);
        await db.SaveChangesAsync(cancellationToken);
        await PublishAsync(notifications, booking.ResourceId, cancellationToken);

        logger.LogInformation("Администратор {AdminId} отклонил бронь {BookingId}", currentUser.Id, id);
        return await LoadAsync(id, cancellationToken);
    }

    /// <summary>
    /// BR-09 и BR-11: время ресурса не занято другой активной бронью, а у пользователя нет своей брони на это же время.
    /// Условие пересечения — то же, что в BookingRules.Overlaps, только выполняется в SQL.
    /// </summary>
    private async Task EnsureNoOverlapsAsync(
        Resource resource, int userId, DateTimeOffset startAt, DateTimeOffset endAt, int exceptBookingId, CancellationToken cancellationToken)
    {
        var busy = await ActiveBookings()
            .Where(b => b.ResourceId == resource.Id && b.Id != exceptBookingId && b.StartAt < endAt && startAt < b.EndAt)
            .OrderBy(b => b.StartAt)
            .Select(b => new { b.StartAt, b.EndAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (busy is not null)
        {
            throw BookingRules.TimeConflict(resource.Name, busy.StartAt, busy.EndAt, policy).ToAppException();
        }

        var own = await ActiveBookings()
            .Where(b => b.UserId == userId && b.Id != exceptBookingId && b.StartAt < endAt && startAt < b.EndAt)
            .OrderBy(b => b.StartAt)
            .Select(b => new { b.StartAt, b.EndAt, ResourceName = b.Resource.Name })
            .FirstOrDefaultAsync(cancellationToken);

        if (own is not null)
        {
            throw BookingRules.UserOverlap(own.ResourceName, own.StartAt, own.EndAt, policy).ToAppException();
        }
    }

    /// <summary>Бронь ждёт согласования: сообщаем владельцу и всем активным администраторам (кроме самого владельца).</summary>
    private async Task<List<Notification>> PendingApprovalNotificationsAsync(
        Booking booking, string resourceName, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var requesterName = await db.Users
            .Where(u => u.Id == booking.UserId)
            .Select(u => u.FullName)
            .SingleAsync(cancellationToken);

        var adminIds = await db.Users
            .Where(u => u.Role == UserRole.Admin && u.IsActive && u.Id != booking.UserId)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);

        var notifications = new List<Notification> { BookingNotifications.PendingApproval(booking, resourceName, policy, now) };
        notifications.AddRange(adminIds.Select(adminId =>
            BookingNotifications.ApprovalRequested(booking, resourceName, requesterName, adminId, policy, now)));

        return notifications;
    }

    private List<Notification> NotifyOwnerUnlessActor(Booking booking, Func<Notification> create) =>
        booking.UserId == currentUser.Id ? [] : [create()];

    /// <summary>После сохранения: уведомления — получателям, «расписание изменилось» — всем, кто смотрит ресурс.</summary>
    private async Task PublishAsync(IReadOnlyCollection<Notification> notifications, int resourceId, CancellationToken cancellationToken)
    {
        await realtime.NotificationsCreatedAsync(notifications, cancellationToken);
        await realtime.ScheduleChangedAsync([resourceId], cancellationToken);
    }

    /// <summary>Активные брони (Pending и Confirmed) — только они занимают время.</summary>
    private IQueryable<Booking> ActiveBookings() =>
        db.Bookings.Where(b => b.Status == BookingStatus.Pending || b.Status == BookingStatus.Confirmed);

    /// <summary>Чужую бронь видит только администратор; для остальных её как будто нет.</summary>
    private void EnsureCanSee([NotNull] Booking? booking)
    {
        if (booking is null || (booking.UserId != currentUser.Id && !currentUser.IsAdmin))
        {
            throw BookingErrors.NotFound();
        }
    }

    private Task<BookingDto> LoadAsync(int id, CancellationToken cancellationToken) =>
        db.Bookings
            .AsNoTracking()
            .Where(b => b.Id == id)
            .Select(BookingMappings.ToDto)
            .SingleAsync(cancellationToken);

    private static IQueryable<Booking> ApplyFilters(IQueryable<Booking> bookings, BookingListQuery query)
    {
        if (query.Status is { } status)
        {
            bookings = bookings.Where(b => b.Status == status);
        }

        // Окно from–to отбирает брони, которые хотя бы частично в него попадают.
        if (query.From is { } from)
        {
            var fromUtc = from.ToUniversalTime();
            bookings = bookings.Where(b => b.EndAt > fromUtc);
        }

        if (query.To is { } to)
        {
            var toUtc = to.ToUniversalTime();
            bookings = bookings.Where(b => b.StartAt < toUtc);
        }

        return bookings;
    }

    private static IOrderedQueryable<Booking> Sort(IQueryable<Booking> bookings, SortDirection direction) =>
        direction == SortDirection.Desc
            ? bookings.OrderByDescending(b => b.StartAt).ThenByDescending(b => b.Id)
            : bookings.OrderBy(b => b.StartAt).ThenBy(b => b.Id);
}
