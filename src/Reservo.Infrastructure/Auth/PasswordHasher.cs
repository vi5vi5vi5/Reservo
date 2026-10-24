using Microsoft.AspNetCore.Identity;
using Reservo.Application.Auth;

namespace Reservo.Infrastructure.Auth;

/// <summary>
/// Обёртка над хешером из ASP.NET Core Identity: PBKDF2 с HMAC-SHA512, случайная соль для каждого пароля
/// и 100 000 итераций. Соль и параметры хранятся прямо внутри строки хеша.
/// </summary>
public sealed class PasswordHasher : IPasswordHasher
{
    // Хешер из Identity обобщён по типу пользователя, но его стандартная реализация самого пользователя не использует.
    private static readonly PasswordHasher<object> Hasher = new();
    private static readonly object NoUser = new();

    public string Hash(string password) => Hasher.HashPassword(NoUser, password);

    public bool Verify(string passwordHash, string password) =>
        Hasher.VerifyHashedPassword(NoUser, passwordHash, password) != PasswordVerificationResult.Failed;
}
