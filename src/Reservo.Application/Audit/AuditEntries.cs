using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Reservo.Domain.Audit;

namespace Reservo.Application.Audit;

/// <summary>Создание записей журнала операций. Подробности сериализуются в JSON для столбца jsonb.</summary>
public static class AuditEntries
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        // Кириллица в JSON остаётся кириллицей, а не превращается в А.
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter() },
    };

    public static AuditLog ForBooking(int? actorId, AuditAction action, int bookingId, object? details, DateTimeOffset now) =>
        new(actorId, action, AuditEntityTypes.Booking, bookingId, Serialize(details), now);

    public static AuditLog ForResource(int? actorId, AuditAction action, int resourceId, object? details, DateTimeOffset now) =>
        new(actorId, action, AuditEntityTypes.Resource, resourceId, Serialize(details), now);

    public static AuditLog ForUser(int? actorId, AuditAction action, int userId, object? details, DateTimeOffset now) =>
        new(actorId, action, AuditEntityTypes.User, userId, Serialize(details), now);

    /// <summary>
    /// Изменения в виде <c>{ "поле": { "old": ..., "new": ... } }</c> — только по полям, которые действительно поменялись.
    /// </summary>
    public static Dictionary<string, object> Changes(params (string Field, object? Old, object? New)[] fields) =>
        fields
            .Where(f => !Equals(f.Old, f.New))
            .ToDictionary(f => f.Field, f => (object)new { old = f.Old, @new = f.New });

    private static string? Serialize(object? details) =>
        details is null ? null : JsonSerializer.Serialize(details, JsonOptions);
}
