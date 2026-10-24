using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Reservo.Application.Common.Errors;

namespace Reservo.Api.ErrorHandling;

/// <summary>
/// Turns expected business errors (<see cref="AppException"/>) into ProblemDetails with the right status code.
/// Anything else falls through to <see cref="GlobalExceptionHandler"/>.
/// </summary>
internal sealed class AppExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<AppExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not AppException error)
        {
            return false;
        }

        // Business errors are normal behaviour, not failures of the app: Information, not Error.
        logger.LogInformation("Request rejected with {Code}: {Message}", error.Code, error.Message);

        var status = error.Kind switch
        {
            ErrorKind.Validation => StatusCodes.Status400BadRequest,
            ErrorKind.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorKind.Forbidden => StatusCodes.Status403Forbidden,
            ErrorKind.NotFound => StatusCodes.Status404NotFound,
            ErrorKind.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest,
        };

        httpContext.Response.StatusCode = status;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = error.Message,
                Extensions = { [ProblemDetailsSetup.CodeKey] = error.Code },
            },
        });
    }
}
