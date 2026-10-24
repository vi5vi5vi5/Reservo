using Reservo.Application.Common.Errors;

namespace Reservo.Application.Bookings;

/// <summary>
/// Ошибки бронирований, которые решаются на уровне приложения (кто действует, существует ли запись).
/// Нарушения самих правил бронирования возвращает доменный BookingRules.
/// </summary>
public static class BookingErrors
{
    /// <summary>Бронь не найдена — или она чужая: существование чужих броней не раскрываем (PROJECT.md, 7.1).</summary>
    public static AppException NotFound() => CommonErrors.NotFound("Бронь не найдена.");

    public static AppException ResourceNotFound(int resourceId) =>
        CommonErrors.ReferenceNotFound($"Ресурс с id {resourceId} не найден.");

    public static AppException OnlyOwnerCanModify() =>
        new(ErrorKind.Forbidden, "forbidden", "Изменять бронь может только её владелец.");

    public static AppException CancelReasonRequired() =>
        new(ErrorKind.Validation, "validation_failed",
            "Укажите причину отмены.",
            "Бронь принадлежит другому пользователю: причину он увидит в уведомлении.");

    /// <summary>
    /// Гонка, которую поймал exclusion constraint в базе: два запроса одновременно прошли проверку в коде,
    /// и записался только первый. Какое именно время заняли, здесь неизвестно — поэтому сообщение общее.
    /// </summary>
    public static AppException TimeConflict() =>
        new(ErrorKind.Conflict, "booking.time_conflict",
            "Ресурс уже занят на это время.",
            "Это время только что забронировали. Обновите расписание и выберите другой слот.");
}
