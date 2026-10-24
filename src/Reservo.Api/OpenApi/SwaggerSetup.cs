using Microsoft.OpenApi;

namespace Reservo.Api.OpenApi;

internal static class SwaggerSetup
{
    private const string BearerScheme = "Bearer";

    public static IServiceCollection AddReservoSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Reservo API",
                Version = "v1",
                Description = "Бронирование аудиторий, лабораторий, переговорных и оборудования.",
            });

            // Descriptions of endpoints and DTO fields come from /// XML comments in the code.
            foreach (var xmlFile in new[] { "Reservo.Api.xml", "Reservo.Application.xml" })
            {
                options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
            }

            // Adds the "Authorize" button: paste the access token once, Swagger sends it with every request.
            options.AddSecurityDefinition(BearerScheme, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Access-токен из ответа /api/auth/login или /api/auth/register.",
            });
            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(BearerScheme, document)] = [],
            });
        });

        return services;
    }
}
