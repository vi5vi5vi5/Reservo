using System.Globalization;
using Reservo.Domain.Catalog;
using Reservo.Domain.Common;

namespace Reservo.Domain.Bookings;

/// <summary>
/// Правила бронирования из PROJECT.md, 5.2 — чистые функции: получают данные, возвращают нарушение
/// или null и ничего не читают из базы. Сервис только подгружает нужное (ресурс, пересекающиеся брони,
/// число активных броней пользователя) и вызывает правила.
/// </summary>
public static class BookingRules
{
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

    /// <summary>
    /// Пересекаются ли интервалы. Они полуоткрытые [start, end), поэтому 10:00–11:00 и 11:00–12:00 не пересекаются.
    /// Тем же условием пользуются LINQ-запросы в сервисах и exclusion constraint в базе.
    /// </summary>
    public static bool Overlaps(DateTimeOffset aStart, DateTimeOffset aEnd, DateTimeOffset bStart, DateTimeOffset bEnd) =>
        aStart < bEnd && bStart < aEnd;

    /// <summary>Все проверки интервала, для которых не нужна база (BR-02–BR-08). Возвращает первое нарушение.</summary>
    public static DomainError? CheckSlot(
        DateTimeOffset startAt, DateTimeOffset endAt, int? attendees, Resource resource, BookingPolicy policy, DateTimeOffset now) =>
        CheckResourceActive(resource)
        ?? CheckTimeGrid(startAt, endAt, policy)
        ?? CheckStartsInFuture(startAt, now)
        ?? CheckAdvance(startAt, now, policy)
        ?? CheckDuration(startAt, endAt, resource, policy)
        ?? CheckWorkingHours(startAt, endAt, resource, policy)
        ?? CheckCapacity(attendees, resource);

    /// <summary>BR-02: начало и конец лежат на сетке с шагом SlotStepMinutes (по местному времени, без секунд).</summary>
    public static DomainError? CheckTimeGrid(DateTimeOffset startAt, DateTimeOffset endAt, BookingPolicy policy)
    {
        var step = TimeSpan.FromMinutes(policy.SlotStepMinutes).Ticks;
        var onGrid = policy.ToLocal(startAt).TimeOfDay.Ticks % step == 0
                     && policy.ToLocal(endAt).TimeOfDay.Ticks % step == 0;

        return onGrid
            ? null
            : new DomainError(
                "booking.invalid_time_grid",
                $"Начало и конец брони должны быть кратны {policy.SlotStepMinutes} минутам, например 10:00, 10:15, 10:30.");
    }

    /// <summary>BR-03: бронь начинается в будущем.</summary>
    public static DomainError? CheckStartsInFuture(DateTimeOffset startAt, DateTimeOffset now) =>
        startAt > now
            ? null
            : new DomainError("booking.in_past", "Нельзя забронировать время, которое уже наступило.");

    /// <summary>BR-04: начало не дальше MaxAdvanceDays от текущего момента.</summary>
    public static DomainError? CheckAdvance(DateTimeOffset startAt, DateTimeOffset now, BookingPolicy policy) =>
        startAt <= now.AddDays(policy.MaxAdvanceDays)
            ? null
            : new DomainError("booking.too_far_in_advance", $"Бронировать можно не дальше чем на {policy.MaxAdvanceDays} дней вперёд.");

    /// <summary>BR-05: длительность от MinDurationMinutes до лимита ресурса (или общего лимита).</summary>
    public static DomainError? CheckDuration(DateTimeOffset startAt, DateTimeOffset endAt, Resource resource, BookingPolicy policy)
    {
        var minutes = (endAt - startAt).TotalMinutes;
        var max = MaxDurationMinutes(resource, policy);

        return minutes >= policy.MinDurationMinutes && minutes <= max
            ? null
            : new DomainError(
                "booking.duration_out_of_range",
                $"{resource.Name}: бронь может длиться от {policy.MinDurationMinutes} до {max} минут.");
    }

    public static int MaxDurationMinutes(Resource resource, BookingPolicy policy) =>
        resource.MaxDurationMinutes ?? policy.DefaultMaxDurationMinutes;

    /// <summary>BR-06: бронь целиком в рабочих часах ресурса и в пределах одного дня — по местному времени площадки.</summary>
    public static DomainError? CheckWorkingHours(DateTimeOffset startAt, DateTimeOffset endAt, Resource resource, BookingPolicy policy)
    {
        var localStart = policy.ToLocal(startAt);
        var localEnd = policy.ToLocal(endAt);

        var fits = localStart.Date == localEnd.Date
                   && TimeOnly.FromTimeSpan(localStart.TimeOfDay) >= resource.OpenTime
                   && TimeOnly.FromTimeSpan(localEnd.TimeOfDay) <= resource.CloseTime;

        return fits
            ? null
            : new DomainError(
                "booking.outside_working_hours",
                $"{resource.Name} работает с {Format(resource.OpenTime)} до {Format(resource.CloseTime)}. "
                + "Бронь должна целиком укладываться в эти часы одного дня.");
    }

    /// <summary>BR-07: ресурс не выведен из эксплуатации.</summary>
    public static DomainError? CheckResourceActive(Resource resource) =>
        resource.IsActive
            ? null
            : new DomainError("resource.inactive", $"{resource.Name}: ресурс выведен из эксплуатации, бронировать его нельзя.");

    /// <summary>BR-08: участников не больше вместимости. Проверяется, только если заданы оба числа.</summary>
    public static DomainError? CheckCapacity(int? attendees, Resource resource) =>
        attendees is null || resource.Capacity is null || attendees <= resource.Capacity
            ? null
            : new DomainError("booking.capacity_exceeded", $"{resource.Name} вмещает не больше {resource.Capacity} человек.");

    /// <summary>BR-09: время ресурса уже занято другой активной бронью.</summary>
    public static DomainError TimeConflict(string resourceName, DateTimeOffset busyFrom, DateTimeOffset busyTo, BookingPolicy policy) =>
        new(
            "booking.time_conflict",
            "Ресурс уже занят на это время.",
            $"{resourceName}: занято {Describe(busyFrom, busyTo, policy)}.",
            IsConflict: true);

    /// <summary>BR-10: у пользователя не больше MaxActiveBookingsPerUser активных будущих броней.</summary>
    public static DomainError? CheckUserLimit(int activeFutureBookings, BookingPolicy policy) =>
        activeFutureBookings < policy.MaxActiveBookingsPerUser
            ? null
            : new DomainError(
                "booking.user_limit_reached",
                $"У вас уже {activeFutureBookings} активных броней — это максимум. Отмените одну из них, чтобы создать новую.");

    /// <summary>BR-11: у пользователя уже есть другая бронь на это время.</summary>
    public static DomainError UserOverlap(string otherResourceName, DateTimeOffset otherFrom, DateTimeOffset otherTo, BookingPolicy policy) =>
        new(
            "booking.user_overlap",
            "У вас уже есть бронь на это время.",
            $"Ваша бронь: {otherResourceName}, {Describe(otherFrom, otherTo, policy)}.",
            IsConflict: true);

    /// <summary>BR-12: изменить можно только активную бронь, которая ещё не началась.</summary>
    public static DomainError? CheckCanModify(Booking booking, DateTimeOffset now)
    {
        if (!booking.Status.IsActive())
        {
            return WrongStatus("Изменить можно только активную бронь", booking);
        }

        return booking.StartAt <= now ? AlreadyStarted("изменить") : null;
    }

    /// <summary>
    /// BR-13: владелец отменяет активную бронь до её начала, администратор — любую активную бронь, пока она не закончилась.
    /// </summary>
    public static DomainError? CheckCanCancel(Booking booking, bool byAdmin, DateTimeOffset now)
    {
        if (!BookingStateMachine.CanTransition(booking.Status, BookingStatus.Cancelled))
        {
            return WrongStatus("Отменить можно только активную бронь", booking);
        }

        if (byAdmin)
        {
            return booking.EndAt <= now
                ? new DomainError("booking.already_finished", "Бронь уже закончилась, отменять её поздно.", IsConflict: true)
                : null;
        }

        return booking.StartAt <= now ? AlreadyStarted("отменить") : null;
    }

    /// <summary>Одобрить можно только заявку на согласовании, которая ещё не началась.</summary>
    public static DomainError? CheckCanApprove(Booking booking, DateTimeOffset now)
    {
        if (!BookingStateMachine.CanTransition(booking.Status, BookingStatus.Confirmed))
        {
            return WrongStatus("Одобрить можно только заявку на согласовании", booking);
        }

        return booking.StartAt <= now ? AlreadyStarted("одобрить") : null;
    }

    /// <summary>Отклонить можно только заявку на согласовании.</summary>
    public static DomainError? CheckCanReject(Booking booking) =>
        BookingStateMachine.CanTransition(booking.Status, BookingStatus.Rejected)
            ? null
            : WrongStatus("Отклонить можно только заявку на согласовании", booking);

    private static DomainError AlreadyStarted(string action) =>
        new("booking.already_started", $"Бронь уже началась — {action} её нельзя.", IsConflict: true);

    /// <summary>«Отменить можно только активную бронь, а эта — «Отменена».»</summary>
    private static DomainError WrongStatus(string rule, Booking booking) =>
        new("booking.invalid_status_transition", $"{rule}, а эта — «{booking.Status.ToRussian()}».", IsConflict: true);

    /// <summary>«25 октября с 14:00 до 15:00» по местному времени площадки.</summary>
    private static string Describe(DateTimeOffset from, DateTimeOffset to, BookingPolicy policy)
    {
        var localFrom = policy.ToLocal(from);
        var localTo = policy.ToLocal(to);
        return $"{localFrom.ToString("d MMMM", Russian)} с {localFrom.ToString("HH:mm", Russian)} до {localTo.ToString("HH:mm", Russian)}";
    }

    private static string Format(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);
}
