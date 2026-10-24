using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Reservo.Application.Common;
using Reservo.Application.Users;
using Reservo.Domain.Users;

namespace Reservo.Application.Auth;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
}

public sealed class AuthService(
    IAppDbContext db,
    IPasswordHasher passwordHasher,
    ITokenService tokenService,
    TimeProvider timeProvider,
    ILogger<AuthService> logger) : IAuthService
{
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var email = User.NormalizeEmail(request.Email);

        // Сначала дружелюбная проверка. Уникальный индекс на users.email — последний рубеж,
        // если две одинаковые регистрации придут в один и тот же момент.
        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            throw AuthErrors.EmailTaken();
        }

        var user = new User(
            email,
            request.FullName,
            passwordHasher.Hash(request.Password),
            UserRole.User,
            timeProvider.GetUtcNow());

        db.Users.Add(user);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Зарегистрирован пользователь {UserId}", user.Id);

        return CreateResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
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

        logger.LogInformation("Пользователь {UserId} вошёл в систему", user.Id);

        return CreateResponse(user);
    }

    private AuthResponse CreateResponse(User user)
    {
        var token = tokenService.CreateAccessToken(user);
        return new AuthResponse(token.Token, token.ExpiresAt, user.ToDto());
    }
}
