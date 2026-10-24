using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Reservo.Application.Auth;
using Reservo.Application.Users;

namespace Reservo.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();

        // Registers every AbstractValidator<T> in this assembly as IValidator<T>.
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}
