using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Reservo.Api.Realtime;

/// <summary>
/// Хаб SignalR для живых обновлений (PROJECT.md, 7.4). Клиент держит с сервером постоянное соединение
/// (обычно WebSocket), и сервер сам присылает события — без опроса «нет ли чего нового».
/// </summary>
/// <remarks>
/// События сервера:
/// <c>notificationReceived</c> — новое уведомление текущему пользователю;
/// <c>scheduleChanged</c> — изменилось расписание ресурса, на который клиент подписан;
/// <c>spaceChanged</c> — изменилась занятость мест коворкинга, на который клиент подписан.
/// </remarks>
[Authorize]
public sealed class LiveHub : Hub
{
    public const string NotificationReceived = "notificationReceived";
    public const string ScheduleChanged = "scheduleChanged";
    public const string SpaceChanged = "spaceChanged";

    /// <summary>Группа SignalR — набор соединений, которым рассылается одно и то же событие.</summary>
    public static string ResourceGroup(int resourceId) => $"resource-{resourceId}";

    public static string SpaceGroup(int spaceId) => $"space-{spaceId}";

    /// <summary>Подписаться на изменения расписания ресурса — например, пока открыта его страница.</summary>
    public Task SubscribeToResource(int resourceId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, ResourceGroup(resourceId));

    /// <summary>Отписаться от изменений расписания ресурса.</summary>
    public Task UnsubscribeFromResource(int resourceId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, ResourceGroup(resourceId));

    /// <summary>Подписаться на занятость мест коворкинга — пока открыта его схема.</summary>
    public Task SubscribeToSpace(int spaceId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, SpaceGroup(spaceId));

    /// <summary>Отписаться от занятости мест коворкинга.</summary>
    public Task UnsubscribeFromSpace(int spaceId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, SpaceGroup(spaceId));
}
