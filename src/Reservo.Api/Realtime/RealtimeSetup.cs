using System.Globalization;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.SignalR;
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
internal sealed class SignalRRealtimeNotifier(IHubContext<LiveHub> hub, ILogger<SignalRRealtimeNotifier> logger) : IRealtimeNotifier
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
        foreach (var resourceId in resourceIds)
        {
            await SendSafelyAsync(() => hub.Clients.Group(LiveHub.ResourceGroup(resourceId))
                .SendAsync(LiveHub.ScheduleChanged, new { resourceId }));
        }
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
