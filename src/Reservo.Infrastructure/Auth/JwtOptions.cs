using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Reservo.Infrastructure.Auth;

/// <summary>Настройки JWT из секции "Jwt" конфигурации.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>Для HMAC-SHA256 нужен ключ не короче 256 бит.</summary>
    public const int MinKeyBytes = 32;

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    /// <summary>Секрет для подписи. Настоящий ключ в Git не попадает: на сервере он приходит из переменной окружения Jwt__Key.</summary>
    public string Key { get; init; } = string.Empty;

    public int AccessTokenMinutes { get; init; } = 30;

    public SymmetricSecurityKey GetSigningKey() => new(Encoding.UTF8.GetBytes(Key));
}
