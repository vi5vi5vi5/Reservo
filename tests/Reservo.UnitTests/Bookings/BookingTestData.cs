using Reservo.Domain.Bookings;
using Reservo.Domain.Catalog;

namespace Reservo.UnitTests.Bookings;

/// <summary>Общие данные для тестов бронирования: московское время, настройки по умолчанию, типовой ресурс.</summary>
internal static class BookingTestData
{
    public static readonly TimeSpan MoscowOffset = TimeSpan.FromHours(3);

    public static readonly BookingPolicy Policy = new(
        SlotStepMinutes: 15,
        MinDurationMinutes: 15,
        DefaultMaxDurationMinutes: 240,
        MaxAdvanceDays: 30,
        MaxActiveBookingsPerUser: 5,
        TimeZone: TimeZoneInfo.FindSystemTimeZoneById("Europe/Moscow"));

    /// <summary>«Сейчас» во всех тестах: 24 октября 2026, 12:00 по Москве.</summary>
    public static readonly DateTimeOffset Now = Msk(24, 12);

    /// <summary>Момент времени по Москве в октябре 2026.</summary>
    public static DateTimeOffset Msk(int day, int hour, int minute = 0, int second = 0) =>
        new(2026, 10, day, hour, minute, second, MoscowOffset);

    /// <summary>Переговорная на 8 человек, работает 08:00–21:00.</summary>
    public static Resource Room(int? capacity = 8, int? maxDurationMinutes = null, bool requiresApproval = false) =>
        new(
            new ResourceDetails(
                CategoryId: 1,
                LocationId: 1,
                Name: "Переговорная «Байкал»",
                Description: null,
                Floor: 2,
                Capacity: capacity,
                ImageUrl: null,
                RequiresApproval: requiresApproval,
                OpenTime: new TimeOnly(8, 0),
                CloseTime: new TimeOnly(21, 0),
                MaxDurationMinutes: maxDurationMinutes),
            Now);

    /// <summary>Бронь на завтра 14:00–15:00, созданная «сейчас».</summary>
    public static Booking TomorrowAt14(bool requiresApproval = false) =>
        new(1, userId: 7, Msk(25, 14), Msk(25, 15), "Встреча", attendees: 4, requiresApproval, Now);
}
