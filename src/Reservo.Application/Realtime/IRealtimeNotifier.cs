using Reservo.Domain.Notifications;

namespace Reservo.Application.Realtime;

/// <summary>
/// Живые обновления для подключённых клиентов (PROJECT.md, 7.4). Реализуется в слое API через SignalR.
/// Вызывается только после того, как изменения сохранены в базе: иначе клиент мог бы узнать о том, чего не случилось.
/// Ошибка доставки не ломает запрос — данные уже в базе, и клиент увидит их при следующей загрузке.
/// </summary>
public interface IRealtimeNotifier
{
    /// <summary>Каждому получателю — его новые уведомления (событие <c>notificationReceived</c>).</summary>
    Task NotificationsCreatedAsync(IReadOnlyCollection<Notification> notifications, CancellationToken cancellationToken);

    /// <summary>Всем, кто смотрит расписание ресурса, — что оно изменилось (событие <c>scheduleChanged</c>).</summary>
    Task ScheduleChangedAsync(IEnumerable<int> resourceIds, CancellationToken cancellationToken);
}
