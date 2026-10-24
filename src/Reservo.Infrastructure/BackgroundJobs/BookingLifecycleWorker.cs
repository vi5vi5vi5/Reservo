using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Reservo.Application.Bookings;
using Reservo.Infrastructure.Bookings;

namespace Reservo.Infrastructure.BackgroundJobs;

/// <summary>
/// Фоновая задача (PROJECT.md, 8.4): раз в минуту отклоняет несогласованные заявки, завершает прошедшие брони
/// и рассылает напоминания. Работает внутри того же процесса, что и API, но не в ответ на запрос пользователя.
/// </summary>
/// <remarks>
/// BackgroundService живёт всё время работы приложения, а DbContext рассчитан на короткую работу.
/// Поэтому на каждом проходе создаётся своя область DI (scope) со своим DbContext и сервисами.
/// </remarks>
internal sealed class BookingLifecycleWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<BookingOptions> options,
    TimeProvider timeProvider,
    ILogger<BookingLifecycleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = options.Value.LifecycleIntervalSeconds;
        if (intervalSeconds <= 0)
        {
            logger.LogInformation("Фоновая задача броней выключена (Booking:LifecycleIntervalSeconds = 0)");
            return;
        }

        var interval = TimeSpan.FromSeconds(intervalSeconds);
        logger.LogInformation("Фоновая задача броней запущена, интервал {Interval}", interval);

        // PeriodicTimer «тикает» раз в интервал и не накапливает пропущенные тики, если проход затянулся.
        using var timer = new PeriodicTimer(interval, timeProvider);

        try
        {
            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Приложение останавливается — это штатное завершение.
        }
    }

    private async Task RunOnceAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var lifecycle = scope.ServiceProvider.GetRequiredService<IBookingLifecycleService>();

            var result = await lifecycle.RunAsync(stoppingToken);
            if (result.Total > 0)
            {
                logger.LogInformation(
                    "Фоновая задача броней: отклонено {Rejected}, завершено {Completed}, напоминаний {Reminded}",
                    result.Rejected, result.Completed, result.Reminded);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Ошибка одного прохода не должна останавливать задачу: через минуту попробуем снова.
            logger.LogError(ex, "Ошибка фоновой задачи броней, следующая попытка через интервал");
        }
    }
}
