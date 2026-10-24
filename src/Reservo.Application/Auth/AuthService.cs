using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Reservo.Application.Audit;
using Reservo.Application.Common;
using Reservo.Application.Users;
using Reservo.Domain.Audit;
using Reservo.Domain.Users;

namespace Reservo.Application.Auth;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken);

    Task LogoutAsync(RefreshTokenRequest request, CancellationToken cancellationToken);
}

/// <remarks>
/// Схема сессии: короткоживущий access-токен (30 минут) для запросов и долгоживущий refresh-токен (14 дней),
/// которым access-токен обновляется без пароля. Refresh-токен одноразовый: при обмене старый отзывается
/// и выдаётся новый (ротация). Если отозванный токен приходит повторно, его, скорее всего, украли —
/// тогда отзываются все сессии пользователя.
/// </remarks>
public sealed class AuthService(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    ICurrentUser currentUser,
    TimeProvider timeProvider,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var email = User.NormalizeEmail(request.Email);
        var passwordHash = passwordHasher.Hash(request.Password);

        // Пользователь, его первая сессия и запись журнала — одной транзакцией: им нужен id нового пользователя.
        var (user, refreshToken) = await db.ExecuteInTransactionAsync(async ct =>
        {
            // Сначала дружелюбная проверка. Уникальный индекс на users.email — последний рубеж,
            // если две одинаковые регистрации придут в один и тот же момент.
            if (await db.Users.AnyAsync(u => u.Email == email, ct))
            {
                throw AuthErrors.EmailTaken();
            }

            var user = new User(email, request.FullName, passwordHash, UserRole.User, now);
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);

            var refreshToken = AddRefreshToken(user.Id, now);
            db.AuditLogs.Add(AuditEntries.ForUser(user.Id, AuditAction.UserRegistered, user.Id, new { user.Email }, now));
            await db.SaveChangesAsync(ct);

            return (user, refreshToken);
        }, cancellationToken);

        logger.LogInformation("Зарегистрирован пользователь {UserId}", user.Id);
        return CreateResponse(user, refreshToken);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var email = User.NormalizeEmail(request.Email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        // Одна и та же ошибка для «нет такого пользователя» и «неверный пароль»: не раскрываем, какие email зарегистрированы.
        if (user is null || !passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            throw AuthErrors.InvalidCredentials();
        }

        if (!user.IsActive)
        {
            throw AuthErrors.UserBlocked();
        }

        var refreshToken = AddRefreshToken(user.Id, now);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Пользователь {UserId} вошёл в систему", user.Id);
        return CreateResponse(user, refreshToken);
    }

    public async Task<AuthResponse> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var hash = tokenService.HashRefreshToken(request.RefreshToken);

        var stored = await db.RefreshTokens
            .Include(t => t.User)
            .SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken)
            ?? throw AuthErrors.InvalidRefreshToken();

        if (stored.RevokedReason == RefreshTokenRevocation.Rotated)
        {
            // Уже обменянный токен пришёл снова: им пользуется ещё кто-то — скорее всего, токен украли.
            // Закрываем все сессии пользователя: и злоумышленнику, и владельцу придётся войти заново.
            await RevokeAllAsync(stored.UserId, now, cancellationToken);
            logger.LogWarning("Повторное использование обменянного refresh-токена пользователя {UserId}: все сессии закрыты", stored.UserId);
            throw AuthErrors.InvalidRefreshToken();
        }

        // BR-16: заблокированный пользователь не может продлить сессию — проверяем раньше, чтобы ответ был понятным.
        if (!stored.User.IsActive)
        {
            throw AuthErrors.UserBlocked();
        }

        // Истёк или отозван выходом либо при подозрении на кражу.
        if (!stored.IsActive(now))
        {
            throw AuthErrors.InvalidRefreshToken();
        }

        // Ротация: старый токен больше не работает, клиент получает новую пару.
        stored.Revoke(now, RefreshTokenRevocation.Rotated);
        var refreshToken = AddRefreshToken(stored.UserId, now);
        await db.SaveChangesAsync(cancellationToken);

        return CreateResponse(stored.User, refreshToken);
    }

    public async Task LogoutAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var hash = tokenService.HashRefreshToken(request.RefreshToken);
        var userId = currentUser.Id;

        // Выйти можно только из своей сессии. Неизвестный или уже отозванный токен — не ошибка: результат тот же.
        var stored = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash && t.UserId == userId, cancellationToken);
        if (stored is null || stored.RevokedAt is not null)
        {
            return;
        }

        stored.Revoke(timeProvider.GetUtcNow(), RefreshTokenRevocation.Logout);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Пользователь {UserId} вышел из системы", userId);
    }

    private NewRefreshToken AddRefreshToken(int userId, DateTimeOffset now)
    {
        var token = tokenService.CreateRefreshToken(now);
        db.RefreshTokens.Add(new RefreshToken(userId, token.Hash, now, token.ExpiresAt));
        return token;
    }

    private async Task RevokeAllAsync(int userId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.RevokedReason, RefreshTokenRevocation.ReuseDetected), cancellationToken);
    }

    private AuthResponse CreateResponse(User user, NewRefreshToken refreshToken)
    {
        var accessToken = tokenService.CreateAccessToken(user);
        return new AuthResponse(accessToken.Token, accessToken.ExpiresAt, refreshToken.Token, refreshToken.ExpiresAt, user.ToDto());
    }
}
