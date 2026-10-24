using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Reservo.Domain.Bookings;

namespace Reservo.Infrastructure.Bookings;

/// <summary>Настройки бронирования из секции "Booking" (PROJECT.md, 5.1).</summary>
public sealed class BookingOptions
{
    public const string SectionName = "Booking";

    public int SlotStepMinutes { get; init; } = 15;

    public int MinDurationMinutes { get; init; } = 15;

    public int DefaultMaxDurationMinutes { get; init; } = 240;

    public int MaxAdvanceDays { get; init; } = 30;

    public int MaxActiveBookingsPerUser { get; init; } = 5;
}

/// <summary>Настройки площадки из секции "Site".</summary>
public sealed class SiteOptions
{
    public const string SectionName = "Site";

    /// <summary>Часовой пояс в формате IANA, например Europe/Moscow.</summary>
    public string TimeZone { get; init; } = "Europe/Moscow";
}

internal static class BookingSettings
{
    /// <summary>
    /// Читает настройки и регистрирует доменную <see cref="BookingPolicy"/> как singleton:
    /// сервисы получают готовые числа и часовой пояс, а не разбирают конфигурацию сами.
    /// </summary>
    public static IServiceCollection AddBookingPolicy(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<BookingOptions>()
            .Bind(configuration.GetSection(BookingOptions.SectionName))
            .Validate(o => o.SlotStepMinutes > 0 && o.MinDurationMinutes > 0 && o.MaxAdvanceDays > 0 && o.MaxActiveBookingsPerUser > 0,
                "Числа в секции Booking должны быть положительными.")
            .Validate(o => o.DefaultMaxDurationMinutes >= o.MinDurationMinutes,
                "Booking:DefaultMaxDurationMinutes не может быть меньше Booking:MinDurationMinutes.")
            .ValidateOnStart();

        services.AddOptions<SiteOptions>()
            .Bind(configuration.GetSection(SiteOptions.SectionName))
            .Validate(o => TimeZoneInfo.TryFindSystemTimeZoneById(o.TimeZone, out _),
                "Site:TimeZone — неизвестный часовой пояс. Нужен идентификатор IANA, например Europe/Moscow.")
            .ValidateOnStart();

        services.AddSingleton(provider =>
        {
            var booking = provider.GetRequiredService<IOptions<BookingOptions>>().Value;
            var site = provider.GetRequiredService<IOptions<SiteOptions>>().Value;

            return new BookingPolicy(
                booking.SlotStepMinutes,
                booking.MinDurationMinutes,
                booking.DefaultMaxDurationMinutes,
                booking.MaxAdvanceDays,
                booking.MaxActiveBookingsPerUser,
                TimeZoneInfo.FindSystemTimeZoneById(site.TimeZone));
        });

        return services;
    }
}
