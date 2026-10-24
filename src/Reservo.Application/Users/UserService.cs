using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Reservo.Application.Audit;
using Reservo.Application.Auth;
using Reservo.Application.Common;
using Reservo.Application.Common.Errors;
using Reservo.Application.Common.Paging;
using Reservo.Domain.Audit;
using Reservo.Domain.Users;

namespace Reservo.Application.Users;

/// <summary>Новая роль пользователя.</summary>
public sealed record ChangeRoleRequest
{
    /// <summary>Роль: <c>User</c> или <c>Admin</c>.</summary>
    /// <example>Admin</example>
    public UserRole? Role { get; init; }
}

public sealed class ChangeRoleRequestValidator : AbstractValidator<ChangeRoleRequest>
{
    public ChangeRoleRequestValidator()
    {
        RuleFor(x => x.Role).NotNull().WithMessage("Укажите роль: User или Admin.").IsInEnum();
    }
}

public interface IUserService
{
    Task<UserDto> GetCurrentAsync(CancellationToken cancellationToken);

    Task<PagedResult<UserDto>> GetListAsync(UserListQuery query, CancellationToken cancellationToken);

    Task<UserDto> ChangeRoleAsync(int id, ChangeRoleRequest request, CancellationToken cancellationToken);

    Task<UserDto> BlockAsync(int id, CancellationToken cancellationToken);

    Task<UserDto> UnblockAsync(int id, CancellationToken cancellationToken);
}

public sealed class UserService(
    IAppDbContext db,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<UserService> logger) : IUserService
{
    public async Task<UserDto> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var user = await db.Users
            .AsNoTracking()
            .SingleOrDefaultAsync(u => u.Id == currentUser.Id, cancellationToken);

        return user?.ToDto() ?? throw CommonErrors.NotFound("Пользователь не найден.");
    }

    public async Task<PagedResult<UserDto>> GetListAsync(UserListQuery query, CancellationToken cancellationToken)
    {
        var users = db.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            users = users.Where(u => u.Email.Contains(term) || u.FullName.ToLower().Contains(term));
        }

        if (query.Role is { } role)
        {
            users = users.Where(u => u.Role == role);
        }

        if (query.IsActive is { } isActive)
        {
            users = users.Where(u => u.IsActive == isActive);
        }

        // Проекция сразу в DTO: SQL выбирает только эти колонки — password_hash никогда.
        return await users
            .OrderBy(u => u.Id)
            .Select(u => new UserDto(u.Id, u.Email, u.FullName, u.Role, u.IsActive, u.CreatedAt))
            .ToPagedResultAsync(query, cancellationToken);
    }

    public async Task<UserDto> ChangeRoleAsync(int id, ChangeRoleRequest request, CancellationToken cancellationToken)
    {
        var user = await FindOtherUserAsync(id, cancellationToken);
        var newRole = request.Role!.Value;

        if (user.Role != newRole)
        {
            var oldRole = user.Role;
            user.ChangeRole(newRole);
            db.AuditLogs.Add(AuditEntries.ForUser(currentUser.Id, AuditAction.UserRoleChanged, id,
                AuditEntries.Changes(("role", oldRole, newRole)), timeProvider.GetUtcNow()));
            await db.SaveChangesAsync(cancellationToken);

            // Новая роль попадёт в access-токен при следующем входе или обмене refresh-токена (не позже чем через 30 минут).
            logger.LogInformation("Администратор {AdminId} сменил роль пользователя {UserId}: {OldRole} → {NewRole}",
                currentUser.Id, id, oldRole, newRole);
        }

        return user.ToDto();
    }

    public async Task<UserDto> BlockAsync(int id, CancellationToken cancellationToken)
    {
        var user = await FindOtherUserAsync(id, cancellationToken);
        if (!user.IsActive)
        {
            return user.ToDto();
        }

        var now = timeProvider.GetUtcNow();
        user.Block();

        // BR-16: все сессии пользователя закрываются — refresh-токены отзываются, продлить вход не получится.
        // Уже выданный access-токен доживёт свои минуты (до 30), это обычная плата за токены без обращения к базе.
        var activeTokens = await db.RefreshTokens.Where(t => t.UserId == id && t.RevokedAt == null).ToListAsync(cancellationToken);
        foreach (var token in activeTokens)
        {
            token.Revoke(now, RefreshTokenRevocation.UserBlocked);
        }

        db.AuditLogs.Add(AuditEntries.ForUser(currentUser.Id, AuditAction.UserBlocked, id, new { revokedSessions = activeTokens.Count }, now));
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Администратор {AdminId} заблокировал пользователя {UserId}", currentUser.Id, id);
        return user.ToDto();
    }

    public async Task<UserDto> UnblockAsync(int id, CancellationToken cancellationToken)
    {
        var user = await FindOtherUserAsync(id, cancellationToken);
        if (user.IsActive)
        {
            return user.ToDto();
        }

        user.Unblock();
        db.AuditLogs.Add(AuditEntries.ForUser(currentUser.Id, AuditAction.UserUnblocked, id, null, timeProvider.GetUtcNow()));
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Администратор {AdminId} разблокировал пользователя {UserId}", currentUser.Id, id);
        return user.ToDto();
    }

    /// <summary>Администратор не может заблокировать себя или сменить себе роль — так можно остаться без администраторов.</summary>
    private async Task<User> FindOtherUserAsync(int id, CancellationToken cancellationToken)
    {
        if (id == currentUser.Id)
        {
            throw new AppException(ErrorKind.Conflict, "user.cannot_change_self",
                "Нельзя заблокировать себя или изменить собственную роль.",
                "Так можно остаться без администраторов. Попросите другого администратора.");
        }

        return await db.Users.FindAsync([id], cancellationToken)
            ?? throw CommonErrors.NotFound("Пользователь не найден.");
    }
}
