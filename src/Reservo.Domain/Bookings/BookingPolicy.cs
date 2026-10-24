using System.Globalization;

namespace Reservo.Domain.Bookings;

/// <summary>
/// Числа, от которых зависят правила бронирования (PROJECT.md, 5.1). Берутся из настроек приложения,
/// поэтому меняются без правки кода.
/// </summary>
/// <param name="SlotStepMinutes">Шаг сетки: начало и конец брони кратны ему.</param>
/// <param name="MinDurationMinutes">Минимальная длительность брони.</param>
/// <param name="DefaultMaxDurationMinutes">Максимальная длительность, если у ресурса не задана своя.</param>
/// <param name="MaxAdvanceDays">На сколько дней вперёд можно бронировать.</param>
/// <param name="MaxActiveBookingsPerUser">Лимит активных будущих броней на пользователя.</param>
/// <param name="ReminderMinutesBefore">За сколько минут до начала брони присылать напоминание.</param>
/// <param name="TimeZone">Часовой пояс площадки: в нём заданы рабочие часы ресурсов.</param>
public sealed record BookingPolicy(
    int SlotStepMinutes,
    int MinDurationMinutes,
    int DefaultMaxDurationMinutes,
    int MaxAdvanceDays,
    int MaxActiveBookingsPerUser,
    int ReminderMinutesBefore,
    TimeZoneInfo TimeZone)
{
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru-RU");

    /// <summary>Переводит момент времени в местное время площадки.</summary>
    public DateTimeOffset ToLocal(DateTimeOffset moment) => TimeZoneInfo.ConvertTime(moment, TimeZone);

    /// <summary>«25 октября с 14:00 до 15:00» по местному времени площадки — для сообщений и уведомлений.</summary>
    public string DescribeInterval(DateTimeOffset from, DateTimeOffset to)
    {
        var localFrom = ToLocal(from);
        var localTo = ToLocal(to);
        return $"{localFrom.ToString("d MMMM", Russian)} с {localFrom.ToString("HH:mm", Russian)} до {localTo.ToString("HH:mm", Russian)}";
    }
}
