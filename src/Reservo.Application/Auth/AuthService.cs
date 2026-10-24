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

        // Friendly check first. The unique index on users.email is the last line of defence
        // if two identical registrations arrive at the same moment.
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

        logger.LogInformation("User {UserId} registered", user.Id);

        return CreateResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = User.NormalizeEmail(request.Email);
        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        // Same error for "no such user" and "wrong password": we don't reveal which emails are registered.
        if (user is null || !passwordHasher.Verify(user.PasswordHash, request.Password))
        {
            throw AuthErrors.InvalidCredentials();
        }

        if (!user.IsActive)
        {
            throw AuthErrors.UserBlocked();
        }

        logger.LogInformation("User {UserId} signed in", user.Id);

        return CreateResponse(user);
    }

    private AuthResponse CreateResponse(User user)
    {
        var token = tokenService.CreateAccessToken(user);
        return new AuthResponse(token.Token, token.ExpiresAt, user.ToDto());
    }
}
