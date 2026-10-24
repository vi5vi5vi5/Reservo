using System.Globalization;
using System.Security.Claims;
using Reservo.Application.Auth;
using Reservo.Domain.Users;

namespace Reservo.Api.Auth;

/// <summary>Reads the current user from the validated JWT of the HTTP request.</summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public int Id =>
        int.TryParse(Principal?.FindFirstValue(ClaimNames.UserId), NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? id
            : throw new InvalidOperationException("The current request has no authenticated user.");

    public bool IsAdmin => Principal?.IsInRole(nameof(UserRole.Admin)) == true;
}
