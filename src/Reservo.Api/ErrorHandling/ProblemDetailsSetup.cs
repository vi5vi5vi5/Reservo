namespace Reservo.Api.ErrorHandling;

/// <summary>
/// Every error response is RFC 9457 ProblemDetails with a machine-readable "code" (PROJECT.md, 7.1).
/// </summary>
internal static class ProblemDetailsSetup
{
    public const string CodeKey = "code";

    public static IServiceCollection AddReservoProblemDetails(this IServiceCollection services)
    {
        services.AddProblemDetails(options => options.CustomizeProblemDetails = context =>
        {
            // Responses without an explicit code (e.g. 404 for an unknown URL) get a default one by status.
            context.ProblemDetails.Extensions.TryAdd(CodeKey, DefaultCode(context.ProblemDetails.Status));
        });

        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }

    private static string DefaultCode(int? status) => status switch
    {
        StatusCodes.Status400BadRequest => "validation_failed",
        StatusCodes.Status401Unauthorized => "unauthorized",
        StatusCodes.Status403Forbidden => "forbidden",
        StatusCodes.Status404NotFound => "not_found",
        StatusCodes.Status405MethodNotAllowed => "method_not_allowed",
        StatusCodes.Status409Conflict => "conflict",
        >= 500 or null => "internal_error",
        _ => $"http_{status}",
    };
}
