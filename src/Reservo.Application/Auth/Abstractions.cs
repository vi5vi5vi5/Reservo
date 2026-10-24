using Reservo.Domain.Users;

namespace Reservo.Application.Auth;

/// <summary>Hashes and verifies passwords. Plain passwords are never stored.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);
}

/// <summary>Issues signed access tokens (JWT).</summary>
public interface ITokenService
{
    AccessToken CreateAccessToken(User user);
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>Who is making the current request. Implemented in the API layer from the JWT claims.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Id of the signed-in user. Throws if the request is anonymous.</summary>
    int Id { get; }

    bool IsAdmin { get; }
}
