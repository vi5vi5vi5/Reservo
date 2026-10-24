using System.Diagnostics.CodeAnalysis;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Reservo.Application.Auth;
using Reservo.Application.Common.Errors;
using Reservo.Infrastructure.Persistence.Configurations;

namespace Reservo.Infrastructure.Persistence;

/// <summary>
/// Database constraints are the last line of defence against races: two requests can pass the same
/// check in code at the same moment, but only one of them gets past the constraint.
/// Its violation is translated into the same business error the service would have thrown,
/// so the client always gets one consistent response.
/// </summary>
internal static class ConstraintErrors
{
    private static readonly Dictionary<string, Func<AppException>> ErrorsByConstraint = new()
    {
        [UserConfiguration.EmailIndexName] = AuthErrors.EmailTaken,
    };

    public static bool TryTranslate(DbUpdateException exception, [NotNullWhen(true)] out AppException? error)
    {
        if (exception.InnerException is PostgresException { ConstraintName: { } constraint }
            && ErrorsByConstraint.TryGetValue(constraint, out var createError))
        {
            error = createError();
            return true;
        }

        error = null;
        return false;
    }
}
