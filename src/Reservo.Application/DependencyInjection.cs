using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Reservo.Application.Auth;
using Reservo.Application.Catalog.Categories;
using Reservo.Application.Catalog.Features;
using Reservo.Application.Catalog.Locations;
using Reservo.Application.Catalog.Resources;
using Reservo.Application.Users;

namespace Reservo.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();

        services.AddScoped<ICategoryService, CategoryService>();
        services.AddScoped<ILocationService, LocationService>();
        services.AddScoped<IFeatureService, FeatureService>();
        services.AddScoped<IResourceService, ResourceService>();

        // Регистрирует каждый AbstractValidator<T> из этой сборки как IValidator<T>.
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
