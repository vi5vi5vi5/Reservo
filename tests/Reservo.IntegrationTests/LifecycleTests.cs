using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Reservo.Application.Bookings;
using Reservo.Application.Common;
using Reservo.Application.Realtime;
using Reservo.Domain.Bookings;
using Reservo.Domain.Notifications;
using Reservo.Infrastructure.Persistence;
using Reservo.IntegrationTests.Infrastructure;

namespace Reservo.IntegrationTests;

/// <summary>
/// Фоновая задача с поддельными часами. Коллекция без параллельности: задача обрабатывает все брони в базе,
/// поэтому такие тесты выполняются отдельно, после остальных, чтобы не задеть их данные во время работы.
/// </summary>
[Collection(nameof(LifecycleCollection))]
public class LifecycleTests(ReservoApiFactory factory)
{
    private readonly TestApi _api = new(factory);

    [Fact]
    public async Task Lifecycle_rejects_completes_and_reminds_once()
    {
        var admin = await _api.LoginAdminAsync();
        var resource = await _api.CreateResourceAsync(admin);
        var user = await _api.RegisterAsync();

        // «Сейчас» для задачи — через 40 дней: дальше всех броней, которые создают другие тесты.
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow.AddDays(40));
        var now = clock.GetUtcNow();

        int remindId, pendingId, finishedId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            // Брони кладём прямо в базу: через API нельзя создать бронь в прошлом — и это правильно.
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var remind = new Booking(resource.Id, user.UserId, now.AddMinutes(20), now.AddMinutes(50), "Скоро начнётся", 2, false, now);
            var pending = new Booking(resource.Id, user.UserId, now.AddMinutes(-10), now.AddMinutes(15), "Не успели согласовать", 2, true, now);
            var finished = new Booking(resource.Id, user.UserId, now.AddHours(-3), now.AddHours(-2), "Уже прошла", 2, false, now);
            db.Bookings.AddRange(remind, pending, finished);
            await db.SaveChangesAsync();
            (remindId, pendingId, finishedId) = (remind.Id, pending.Id, finished.Id);
        }

        await RunLifecycleAsync(clock);

        Assert.Equal(BookingStatus.Rejected, await StatusAsync(pendingId));
        Assert.Equal(BookingStatus.Completed, await StatusAsync(finishedId));
        Assert.Equal(BookingStatus.Confirmed, await StatusAsync(remindId));
        Assert.Equal(1, await NotificationCountAsync(user.UserId, NotificationType.BookingReminder));
        Assert.Equal(1, await NotificationCountAsync(user.UserId, NotificationType.BookingRejected));

        // Через 5 минут напоминание не повторяется.
        clock.Advance(TimeSpan.FromMinutes(5));
        await RunLifecycleAsync(clock);
        Assert.Equal(1, await NotificationCountAsync(user.UserId, NotificationType.BookingReminder));

        // Через час и эта бронь прошла — задача её завершает.
        clock.Advance(TimeSpan.FromHours(1));
        await RunLifecycleAsync(clock);
        Assert.Equal(BookingStatus.Completed, await StatusAsync(remindId));
    }

    /// <summary>Тот же сервис, что вызывает фоновая задача, но с поддельными часами вместо системных.</summary>
    private async Task RunLifecycleAsync(TimeProvider clock)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var lifecycle = new BookingLifecycleService(
            services.GetRequiredService<IAppDbContext>(),
            services.GetRequiredService<BookingPolicy>(),
            clock,
            services.GetRequiredService<IRealtimeNotifier>());

        await lifecycle.RunAsync(CancellationToken.None);
    }

    private async Task<BookingStatus> StatusAsync(int bookingId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Bookings.Where(b => b.Id == bookingId).Select(b => b.Status).SingleAsync();
    }

    private async Task<int> NotificationCountAsync(int userId, NotificationType type)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Notifications.CountAsync(n => n.UserId == userId && n.Type == type);
    }
}

/// <summary>Коллекция тестов, которые выполняются по одному и после всех параллельных.</summary>
[CollectionDefinition(nameof(LifecycleCollection), DisableParallelization = true)]
public sealed class LifecycleCollection;
