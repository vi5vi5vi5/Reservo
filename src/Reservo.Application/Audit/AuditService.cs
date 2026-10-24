using System.Text.Json;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Reservo.Application.Auth;
using Reservo.Application.Bookings;
using Reservo.Application.Common;
using Reservo.Application.Common.Paging;
using Reservo.Domain.Audit;

namespace Reservo.Application.Audit;

/// <summary>Запись журнала операций.</summary>
/// <param name="Id">Идентификатор записи.</param>
/// <param name="Action">Что произошло, например <c>BookingUpdated</c>.</param>
/// <param name="EntityType">С чем: <c>Booking</c>, <c>Resource</c> или <c>User</c>.</param>
/// <param name="EntityId">Id этой сущности.</param>
/// <param name="ActorId">Кто сделал; пусто — система (фоновая задача).</param>
/// <param name="ActorName">Имя того, кто сделал.</param>
/// <param name="Details">Подробности: старые и новые значения, причина и т. п.</param>
/// <param name="CreatedAt">Когда (UTC).</param>
public sealed record AuditLogDto(
    int Id,
    AuditAction Action,
    string EntityType,
    int EntityId,
    int? ActorId,
    string? ActorName,
    JsonElement? Details,
    DateTimeOffset CreatedAt);

/// <summary>Фильтры журнала операций.</summary>
public sealed class AuditListQuery : PagedQuery
{
    /// <summary>Только действия этого пользователя.</summary>
    public int? ActorId { get; init; }

    /// <summary>Только записи о сущностях этого типа: <c>Booking</c>, <c>Resource</c> или <c>User</c>.</summary>
    public string? EntityType { get; init; }

    /// <summary>Только записи об этой сущности (вместе с <c>entityType</c>).</summary>
    public int? EntityId { get; init; }

    /// <summary>Только это действие.</summary>
    public AuditAction? Action { get; init; }

    /// <summary>Не раньше этого момента.</summary>
    public DateTimeOffset? From { get; init; }

    /// <summary>Раньше этого момента.</summary>
    public DateTimeOffset? To { get; init; }
}

public sealed class AuditListQueryValidator : AbstractValidator<AuditListQuery>
{
    private static readonly string[] EntityTypes = [AuditEntityTypes.Booking, AuditEntityTypes.Resource, AuditEntityTypes.User];

    public AuditListQueryValidator()
    {
        Include(new PagedQueryValidator());

        RuleFor(x => x.EntityType)
            .Must(type => EntityTypes.Contains(type))
            .WithMessage($"Тип сущности — один из: {string.Join(", ", EntityTypes)}.")
            .When(x => x.EntityType is not null);

        RuleFor(x => x.To)
            .Must((query, to) => to > query.From).WithMessage("Конец интервала должен быть позже начала.")
            .When(x => x.From is not null && x.To is not null);
    }
}

public interface IAuditService
{
    Task<IReadOnlyList<AuditLogDto>> GetBookingHistoryAsync(int bookingId, CancellationToken cancellationToken);

    Task<PagedResult<AuditLogDto>> GetLogAsync(AuditListQuery query, CancellationToken cancellationToken);
}

public sealed class AuditService(IAppDbContext db, ICurrentUser currentUser) : IAuditService
{
    public async Task<IReadOnlyList<AuditLogDto>> GetBookingHistoryAsync(int bookingId, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var isAdmin = currentUser.IsAdmin;

        // Историю брони видят её владелец и администратор; для остальных брони как будто нет.
        if (!await db.Bookings.AnyAsync(b => b.Id == bookingId && (isAdmin || b.UserId == userId), cancellationToken))
        {
            throw BookingErrors.NotFound();
        }

        var rows = await Project(db.AuditLogs
                .AsNoTracking()
                .Where(a => a.EntityType == AuditEntityTypes.Booking && a.EntityId == bookingId)
                .OrderBy(a => a.CreatedAt)
                .ThenBy(a => a.Id))
            .ToListAsync(cancellationToken);

        return rows.Select(ToDto).ToList();
    }

    public async Task<PagedResult<AuditLogDto>> GetLogAsync(AuditListQuery query, CancellationToken cancellationToken)
    {
        var logs = db.AuditLogs.AsNoTracking();

        if (query.ActorId is { } actorId)
        {
            logs = logs.Where(a => a.ActorId == actorId);
        }

        if (query.EntityType is { } entityType)
        {
            logs = logs.Where(a => a.EntityType == entityType);
        }

        if (query.EntityId is { } entityId)
        {
            logs = logs.Where(a => a.EntityId == entityId);
        }

        if (query.Action is { } action)
        {
            logs = logs.Where(a => a.Action == action);
        }

        if (query.From is { } from)
        {
            var fromUtc = from.ToUniversalTime();
            logs = logs.Where(a => a.CreatedAt >= fromUtc);
        }

        if (query.To is { } to)
        {
            var toUtc = to.ToUniversalTime();
            logs = logs.Where(a => a.CreatedAt < toUtc);
        }

        // Свежие записи сверху.
        var page = await Project(logs.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.Id))
            .ToPagedResultAsync(query, cancellationToken);

        return new PagedResult<AuditLogDto>(page.Items.Select(ToDto).ToList(), page.Page, page.PageSize, page.TotalCount);
    }

    private sealed record AuditRow(
        int Id, AuditAction Action, string EntityType, int EntityId, int? ActorId, string? ActorName, string? Details, DateTimeOffset CreatedAt);

    private static IQueryable<AuditRow> Project(IQueryable<AuditLog> logs) =>
        logs.Select(a => new AuditRow(
            a.Id,
            a.Action,
            a.EntityType,
            a.EntityId,
            a.ActorId,
            a.Actor != null ? a.Actor.FullName : null,
            a.Details,
            a.CreatedAt));

    /// <summary>Подробности хранятся строкой JSON; в ответе они становятся настоящим вложенным объектом.</summary>
    private static AuditLogDto ToDto(AuditRow row) => new(
        row.Id,
        row.Action,
        row.EntityType,
        row.EntityId,
        row.ActorId,
        row.ActorName,
        row.Details is null ? null : JsonSerializer.Deserialize<JsonElement>(row.Details),
        row.CreatedAt);
}
