using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Reservo.Application.Common;
using Reservo.Application.Auth;
using Reservo.Application.Notifications;
using Reservo.Application.Realtime;
using Reservo.Domain.Notifications;

namespace Reservo.Api.Realtime;

internal static class RealtimeSetup
{
    public const string HubPath = "/hubs/live";

    public static IServiceCollection AddReservoRealtime(this IServiceCollection services)
    {
        services.AddSignalR()
            // Перечисления в событиях — текстом, как и в REST API: "type": "BookingApproved".
            .AddJsonProtocol(options => options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddSingleton<IUserIdProvider, JwtUserIdProvider>();
        services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();

        return services;
    }
}

/// <summary>
/// Как SignalR узнаёт, какому пользователю принадлежит соединение: по claim "sub" из JWT.
/// Благодаря этому Clients.User("7") доставляет событие во все вкладки и устройства пользователя 7.
/// </summary>
internal sealed class JwtUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection) => connection.User.FindFirst(ClaimNames.UserId)?.Value;
}

/// <summary>Доставка живых обновлений через SignalR. Вызывается сервисами после сохранения изменений в базе.</summary>
/// <remarks>
/// Сервис-одиночка (singleton), а база — на запрос (scoped), поэтому коворкинги мест он узнаёт через свою короткую область DI.
/// Так сервисам не нужно знать, что у ресурса-места есть ещё и группа коворкинга.
/// </remarks>
internal sealed class SignalRRealtimeNotifier(
    IHubContext<LiveHub> hub,
    IServiceScopeFactory scopeFactory,
    ILogger<SignalRRealtimeNotifier> logger) : IRealtimeNotifier
{
    // Токен отмены запроса здесь не используется: изменения уже в базе, и если клиент, отправивший запрос,
    // закрыл соединение, остальные получатели всё равно должны узнать о них.
    public async Task NotificationsCreatedAsync(IReadOnlyCollection<Notification> notifications, CancellationToken cancellationToken)
    {
        foreach (var notification in notifications)
        {
            var userId = notification.UserId.ToString(CultureInfo.InvariantCulture);
            await SendSafelyAsync(() => hub.Clients.User(userId).SendAsync(LiveHub.NotificationReceived, notification.ToDto()));
        }
    }

    public async Task ScheduleChangedAsync(IEnumerable<int> resourceIds, CancellationToken cancellationToken)
    {
        var ids = resourceIds.Distinct().ToArray();
        foreach (var resourceId in ids)
        {
            await SendSafelyAsync(() => hub.Clients.Group(LiveHub.ResourceGroup(resourceId))
                .SendAsync(LiveHub.ScheduleChanged, new { resourceId }));
        }

        // Если среди ресурсов есть места, схему их коворкинга тоже пора обновить.
        await SendSafelyAsync(async () =>
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var spaceIds = await db.Resources
                .Where(r => ids.Contains(r.Id) && r.SpaceId != null)
                .Select(r => r.SpaceId!.Value)
                .Distinct()
                .ToListAsync(CancellationToken.None);

            foreach (var spaceId in spaceIds)
            {
                await hub.Clients.Group(LiveHub.SpaceGroup(spaceId)).SendAsync(LiveHub.SpaceChanged, new { spaceId });
            }
        });
    }

    /// <summary>Сбой доставки не должен ломать запрос: данные уже сохранены, клиент увидит их при следующей загрузке.</summary>
    private async Task SendSafelyAsync(Func<Task> send)
    {
        try
        {
            await send();
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Не удалось отправить живое обновление через SignalR");
        }
    }
}
