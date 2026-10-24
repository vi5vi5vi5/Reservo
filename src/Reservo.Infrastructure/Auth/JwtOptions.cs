using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Reservo.Infrastructure.Auth;

/// <summary>JWT settings from the "Jwt" configuration section.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>HMAC-SHA256 needs a key of at least 256 bits.</summary>
    public const int MinKeyBytes = 32;

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    /// <summary>Signing secret. Never commit a real one: on a server it comes from the Jwt__Key environment variable.</summary>
    public string Key { get; init; } = string.Empty;

    public int AccessTokenMinutes { get; init; } = 30;

    public SymmetricSecurityKey GetSigningKey() => new(Encoding.UTF8.GetBytes(Key));
}
