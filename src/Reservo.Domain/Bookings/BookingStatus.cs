namespace Reservo.Domain.Bookings;

public enum BookingStatus
{
    /// <summary>Ждёт решения администратора.</summary>
    Pending,

    /// <summary>Подтверждена.</summary>
    Confirmed,

    /// <summary>Отклонена администратором или не согласована вовремя.</summary>
    Rejected,

    /// <summary>Отменена пользователем, администратором или системой.</summary>
    Cancelled,

    /// <summary>Время брони прошло.</summary>
    Completed,
}

public static class BookingStatusExtensions
{
    /// <summary>Активные брони занимают время ресурса: только с ними возможен конфликт.</summary>
    public static bool IsActive(this BookingStatus status) => status is BookingStatus.Pending or BookingStatus.Confirmed;

    public static string ToRussian(this BookingStatus status) => status switch
    {
        BookingStatus.Pending => "На согласовании",
        BookingStatus.Confirmed => "Подтверждена",
        BookingStatus.Rejected => "Отклонена",
        BookingStatus.Cancelled => "Отменена",
        BookingStatus.Completed => "Завершена",
        _ => status.ToString(),
    };
}
