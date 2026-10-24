using Reservo.Domain.Users;

namespace Reservo.Domain.Audit;

/// <summary>Что произошло (PROJECT.md, 6.3).</summary>
public enum AuditAction
{
    UserRegistered,
    UserRoleChanged,
    UserBlocked,
    UserUnblocked,
    ResourceCreated,
    ResourceUpdated,
    ResourceDeactivated,
    ResourceActivated,
    BookingCreated,
    BookingUpdated,
    BookingApproved,
    BookingRejected,
    BookingCancelled,
    BookingCompleted,
}

/// <summary>С какой сущностью произошло. Строки, а не перечисление: так записи в базе читаемы и фильтровать по ним просто.</summary>
public static class AuditEntityTypes
{
    public const string Booking = "Booking";
    public const string Resource = "Resource";
    public const string User = "User";
}

/// <summary>
/// Запись журнала операций: кто, что, над чем и когда сделал (PROJECT.md, 5.6).
/// Одна таблица закрывает и историю брони для пользователя, и общий журнал для администратора.
/// </summary>
public class AuditLog
{
    public const int EntityTypeMaxLength = 50;

    // Нужен EF Core при загрузке из базы.
    private AuditLog()
    {
    }

    /// <param name="actorId">Кто сделал. null — система (фоновая задача).</param>
    /// <param name="details">Подробности в JSON: старые и новые значения, причина и т. п.</param>
    public AuditLog(int? actorId, AuditAction action, string entityType, int entityId, string? details, DateTimeOffset createdAt)
    {
        ActorId = actorId;
        Action = action;
        EntityType = entityType;
        EntityId = entityId;
        Details = details;
        CreatedAt = createdAt;
    }

    public int Id { get; private set; }

    public int? ActorId { get; private set; }

    public User? Actor { get; private set; }

    public AuditAction Action { get; private set; }

    public string EntityType { get; private set; } = null!;

    public int EntityId { get; private set; }

    /// <summary>JSON, хранится в столбце jsonb.</summary>
    public string? Details { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
