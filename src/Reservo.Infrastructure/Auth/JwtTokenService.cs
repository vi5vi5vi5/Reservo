using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Reservo.Application.Auth;
using Reservo.Domain.Users;

namespace Reservo.Infrastructure.Auth;

public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : ITokenService
{
    private const int RefreshTokenBytes = 32;

    private readonly JwtOptions _options = options.Value;
    private readonly JsonWebTokenHandler _handler = new();

    public AccessToken CreateAccessToken(User user)
    {
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(_options.AccessTokenMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Claims = new Dictionary<string, object>
            {
                [ClaimNames.UserId] = user.Id.ToString(CultureInfo.InvariantCulture),
                [ClaimNames.Email] = user.Email,
                [ClaimNames.Name] = user.FullName,
                [ClaimNames.Role] = user.Role.ToString(),
            },
            SigningCredentials = new SigningCredentials(_options.GetSigningKey(), SecurityAlgorithms.HmacSha256),
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }

    /// <summary>
    /// 32 случайных байта из криптографического генератора — угадать такой токен невозможно.
    /// В отличие от JWT, он ничего не содержит: это просто ключ к записи в таблице refresh_tokens.
    /// </summary>
    public NewRefreshToken CreateRefreshToken(DateTimeOffset now)
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(RefreshTokenBytes));
        return new NewRefreshToken(token, HashRefreshToken(token), now.AddDays(_options.RefreshTokenDays));
    }

    /// <summary>SHA-256: если базу украдут, по хешу восстановить токен нельзя.</summary>
    public string HashRefreshToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
