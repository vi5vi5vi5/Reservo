using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Reservo.Application.Auth;
using Reservo.Application.Common;
using Reservo.Application.Common.Errors;
using Reservo.Application.Common.Paging;
using Reservo.Domain.Notifications;

namespace Reservo.Application.Notifications;

/// <summary>Уведомление.</summary>
/// <param name="Id">Идентификатор.</param>
/// <param name="Type">Повод, например <c>BookingApproved</c>.</param>
/// <param name="Title">Заголовок.</param>
/// <param name="Message">Текст.</param>
/// <param name="BookingId">Бронь, к которой относится уведомление.</param>
/// <param name="IsRead">Прочитано ли.</param>
/// <param name="CreatedAt">Когда создано (UTC).</param>
public sealed record NotificationDto(
    int Id, NotificationType Type, string Title, string Message, int? BookingId, bool IsRead, DateTimeOffset CreatedAt);

/// <summary>Число непрочитанных уведомлений.</summary>
/// <param name="Count">Сколько непрочитанных.</param>
public sealed record UnreadCountDto(int Count);

/// <summary>Фильтры списка уведомлений.</summary>
public sealed class NotificationListQuery : PagedQuery
{
    /// <summary>Только непрочитанные.</summary>
    public bool UnreadOnly { get; init; }
}

public sealed class NotificationListQueryValidator : AbstractValidator<NotificationListQuery>
{
    public NotificationListQueryValidator() => Include(new PagedQueryValidator());
}

public static class NotificationMappings
{
    public static NotificationDto ToDto(this Notification n) =>
        new(n.Id, n.Type, n.Title, n.Message, n.BookingId, n.IsRead, n.CreatedAt);
}

public interface INotificationService
{
    Task<PagedResult<NotificationDto>> GetAsync(NotificationListQuery query, CancellationToken cancellationToken);

    Task<UnreadCountDto> GetUnreadCountAsync(CancellationToken cancellationToken);

    Task MarkReadAsync(int id, CancellationToken cancellationToken);

    Task MarkAllReadAsync(CancellationToken cancellationToken);
}

/// <summary>Свои уведомления: каждый пользователь видит и отмечает только адресованные ему.</summary>
public sealed class NotificationService(IAppDbContext db, ICurrentUser currentUser) : INotificationService
{
    public async Task<PagedResult<NotificationDto>> GetAsync(NotificationListQuery query, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var notifications = db.Notifications.AsNoTracking().Where(n => n.UserId == userId);

        if (query.UnreadOnly)
        {
            notifications = notifications.Where(n => !n.IsRead);
        }

        // Свежие сверху.
        return await notifications
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Select(n => new NotificationDto(n.Id, n.Type, n.Title, n.Message, n.BookingId, n.IsRead, n.CreatedAt))
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<UnreadCountDto> GetUnreadCountAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        return new UnreadCountDto(await db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken));
    }

    public async Task MarkReadAsync(int id, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var notification = await db.Notifications.SingleOrDefaultAsync(n => n.Id == id && n.UserId == userId, cancellationToken)
            ?? throw CommonErrors.NotFound("Уведомление не найдено.");

        notification.MarkRead();
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllReadAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;

        // Один SQL-запрос UPDATE ... WHERE, без загрузки уведомлений в память.
        await db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true), cancellationToken);
    }
}
