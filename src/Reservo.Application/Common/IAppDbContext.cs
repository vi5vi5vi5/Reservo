using Microsoft.EntityFrameworkCore;
using Reservo.Domain.Audit;
using Reservo.Domain.Bookings;
using Reservo.Domain.Catalog;
using Reservo.Domain.Notifications;
using Reservo.Domain.Users;

namespace Reservo.Application.Common;

/// <summary>
/// База данных глазами сервисов приложения. Реализуется классом AppDbContext в Infrastructure.
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    DbSet<Category> Categories { get; }

    DbSet<Location> Locations { get; }

    DbSet<Feature> Features { get; }

    DbSet<Resource> Resources { get; }

    DbSet<Booking> Bookings { get; }

    DbSet<AuditLog> AuditLogs { get; }

    DbSet<Notification> Notifications { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Выполняет операцию в одной транзакции базы: сохраняется либо всё, что она сделала, либо ничего.
    /// Нужна, когда операции требуется несколько SaveChanges — например, сначала сохранить бронь, чтобы получить
    /// её id, а затем записать журнал и уведомления с этим id.
    /// </summary>
    /// <remarks>
    /// При временном сбое (обрыв связи, взаимоблокировка) операция повторяется целиком с чистого листа:
    /// всё, что было загружено раньше, забывается. Поэтому всё, что операция читает из базы и создаёт,
    /// должно происходить внутри неё.
    /// </remarks>
    Task<T> ExecuteInTransactionAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);

    /// <summary>
    /// Ставит текущую транзакцию в очередь на бронирование ресурса: пока она не завершится, другие транзакции,
    /// которые бронируют тот же ресурс, ждут. Вызывается только внутри <see cref="ExecuteInTransactionAsync{T}"/>.
    /// </summary>
    /// <remarks>
    /// Без очереди одновременные брони одного ресурса проходят проверку в коде вместе, а потом exclusion constraint
    /// заставляет их ждать друг друга — и PostgreSQL прерывает их как взаимоблокировку. С очередью следующая бронь
    /// видит предыдущую уже сохранённой, и проверка в коде честно отвечает «занято». Брони разных ресурсов не мешают друг другу.
    /// </remarks>
    Task LockResourceForBookingAsync(int resourceId, CancellationToken cancellationToken);
}
