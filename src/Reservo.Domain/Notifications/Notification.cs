namespace Reservo.Domain.Notifications;

/// <summary>Повод для уведомления (PROJECT.md, 5.5).</summary>
public enum NotificationType
{
    /// <summary>Бронь создана и сразу подтверждена.</summary>
    BookingConfirmed,

    /// <summary>Бронь создана или перенесена и ждёт согласования.</summary>
    BookingPendingApproval,

    /// <summary>Администраторам: появилась заявка на согласование.</summary>
    ApprovalRequested,

    /// <summary>Администратор одобрил заявку.</summary>
    BookingApproved,

    /// <summary>Заявку отклонил администратор или система (не успели согласовать).</summary>
    BookingRejected,

    /// <summary>Бронь отменил администратор или ресурс выведен из эксплуатации.</summary>
    BookingCancelled,

    /// <summary>Скоро начало брони.</summary>
    BookingReminder,
}

/// <summary>Уведомление пользователю. Хранится в базе и в реальном времени отправляется через SignalR.</summary>
public class Notification
{
    public const int TitleMaxLength = 200;
    public const int MessageMaxLength = 1000;

    // Нужен EF Core при загрузке из базы.
    private Notification()
    {
    }

    public Notification(int userId, int? bookingId, NotificationType type, string title, string message, DateTimeOffset createdAt)
    {
        UserId = userId;
        BookingId = bookingId;
        Type = type;
        Title = title;
        Message = message.Length > MessageMaxLength ? message[..MessageMaxLength] : message;
        CreatedAt = createdAt;
    }

    public int Id { get; private set; }

    /// <summary>Получатель.</summary>
    public int UserId { get; private set; }

    public int? BookingId { get; private set; }

    public NotificationType Type { get; private set; }

    public string Title { get; private set; } = null!;

    public string Message { get; private set; } = null!;

    public bool IsRead { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public void MarkRead() => IsRead = true;
}
