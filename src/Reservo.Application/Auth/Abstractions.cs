using Reservo.Domain.Users;

namespace Reservo.Application.Auth;

/// <summary>Хеширует и проверяет пароли. Пароли в открытом виде никогда не хранятся.</summary>
public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string passwordHash, string password);
}

/// <summary>Выдаёт подписанные access-токены (JWT).</summary>
public interface ITokenService
{
    AccessToken CreateAccessToken(User user);
}

public sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>Кто выполняет текущий запрос. Реализуется в слое API по claims из JWT.</summary>
public interface ICurrentUser
{
    bool IsAuthenticated { get; }

    /// <summary>Id вошедшего пользователя. Для анонимного запроса выбрасывает исключение.</summary>
    int Id { get; }

    bool IsAdmin { get; }
}
