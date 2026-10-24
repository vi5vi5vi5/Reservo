using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Reservo.Application.Auth;
using Reservo.Domain.Users;
using Reservo.Infrastructure.Auth;

namespace Reservo.Api.Auth;

internal static class AuthSetup
{
    public static IServiceCollection AddReservoAuth(this IServiceCollection services)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        // Токены проверяются теми же JwtOptions, которыми они были подписаны.
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                // Оставляем имена claims как в токене ("sub", "role"), без устаревшего переименования.
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = jwt.GetSigningKey(),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    NameClaimType = ClaimNames.Name,
                    RoleClaimType = ClaimNames.Role,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        services.AddAuthorizationBuilder()
            // Всё закрыто по умолчанию: любой эндпоинт требует входа, если он не помечен [AllowAnonymous].
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.Admin, policy => policy.RequireRole(nameof(UserRole.Admin)));

        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpCurrentUser>();

        return services;
    }
}
