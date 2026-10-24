using Reservo.Domain.Catalog;
using Reservo.Domain.Users;

namespace Reservo.Domain.Bookings;

/// <summary>Бронь: занятие ресурса пользователем на интервал [StartAt, EndAt).</summary>
/// <remarks>
/// Бизнес-правила (свободно ли время, рабочие часы и т. д.) проверяет <see cref="BookingRules"/> до вызова методов.
/// Сама сущность охраняет только стейт-машину: недопустимый переход здесь означает ошибку в коде,
/// поэтому выбрасывается InvalidOperationException (ответ 500), а не бизнес-ошибка.
/// </remarks>
public class Booking
{
    public const int TitleMaxLength = 200;
    public const int StatusReasonMaxLength = 500;

    // Нужен EF Core при загрузке из базы.
    private Booking()
    {
    }

    /// <param name="requiresApproval">Ресурс требует согласования: бронь создаётся в статусе Pending (BR-14).</param>
    public Booking(
        int resourceId,
        int userId,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        string title,
        int? attendees,
        bool requiresApproval,
        DateTimeOffset now)
    {
        ResourceId = resourceId;
        UserId = userId;
        SetDetails(startAt, endAt, title, attendees);
        Status = requiresApproval ? BookingStatus.Pending : BookingStatus.Confirmed;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public int Id { get; private set; }

    public int ResourceId { get; private set; }

    public Resource Resource { get; private set; } = null!;

    public int UserId { get; private set; }

    public User User { get; private set; } = null!;

    /// <summary>Начало, UTC.</summary>
    public DateTimeOffset StartAt { get; private set; }

    /// <summary>Конец (не включительно), UTC.</summary>
    public DateTimeOffset EndAt { get; private set; }

    /// <summary>Тема или цель брони.</summary>
    public string Title { get; private set; } = null!;

    public int? Attendees { get; private set; }

    public BookingStatus Status { get; private set; }

    /// <summary>Причина отмены или отклонения.</summary>
    public string? StatusReason { get; private set; }

    /// <summary>Когда отправлено напоминание. Нужно, чтобы не отправить его дважды.</summary>
    public DateTimeOffset? ReminderSentAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    /// <summary>
    /// Меняет время, тему и число участников. Если у ресурса с согласованием перенесли время
    /// подтверждённой брони, она снова уходит на согласование (BR-14).
    /// </summary>
    public void Update(DateTimeOffset startAt, DateTimeOffset endAt, string title, int? attendees, bool requiresApproval, DateTimeOffset now)
    {
        EnsureActive();

        // DateTimeOffset сравнивается по моменту времени, а не по записи: 14:00+03:00 == 11:00Z.
        var timeChanged = startAt != StartAt || endAt != EndAt;

        SetDetails(startAt, endAt, title, attendees);

        if (timeChanged && requiresApproval && Status == BookingStatus.Confirmed)
        {
            TransitionTo(BookingStatus.Pending);
        }

        UpdatedAt = now;
    }

    public void Approve(DateTimeOffset now)
    {
        TransitionTo(BookingStatus.Confirmed);
        UpdatedAt = now;
    }

    public void Reject(string reason, DateTimeOffset now)
    {
        TransitionTo(BookingStatus.Rejected);
        StatusReason = reason.Trim();
        UpdatedAt = now;
    }

    public void Cancel(string? reason, DateTimeOffset now)
    {
        TransitionTo(BookingStatus.Cancelled);
        StatusReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        UpdatedAt = now;
    }

    public void Complete(DateTimeOffset now)
    {
        TransitionTo(BookingStatus.Completed);
        UpdatedAt = now;
    }

    private void SetDetails(DateTimeOffset startAt, DateTimeOffset endAt, string title, int? attendees)
    {
        // В базе время хранится только в UTC: Npgsql и не примет другое смещение.
        StartAt = startAt.ToUniversalTime();
        EndAt = endAt.ToUniversalTime();
        Title = title.Trim();
        Attendees = attendees;
    }

    private void EnsureActive()
    {
        if (!Status.IsActive())
        {
            throw new InvalidOperationException($"Бронь {Id} в статусе {Status} изменять нельзя.");
        }
    }

    private void TransitionTo(BookingStatus target)
    {
        if (!BookingStateMachine.CanTransition(Status, target))
        {
            throw new InvalidOperationException($"Недопустимый переход брони {Id}: {Status} → {target}.");
        }

        Status = target;
    }
}
