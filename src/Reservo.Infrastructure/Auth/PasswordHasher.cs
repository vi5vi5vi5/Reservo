using Microsoft.AspNetCore.Identity;
using Reservo.Application.Auth;

namespace Reservo.Infrastructure.Auth;

/// <summary>
/// Wraps the ASP.NET Core Identity hasher: PBKDF2 with HMAC-SHA512, a random salt per password
/// and 100 000 iterations. The salt and parameters are stored inside the hash string itself.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    // The Identity hasher is generic over a user type but its default implementation ignores the user.
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly object NoUser = new();

    public string Hash(string password) => Hasher.HashPassword(NoUser, password);

    public bool Verify(string passwordHash, string password) =>
        Hasher.VerifyHashedPassword(NoUser, passwordHash, password) != PasswordVerificationResult.Failed;
}
