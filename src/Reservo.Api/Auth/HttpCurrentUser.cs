using System.Globalization;
using System.Security.Claims;
using Reservo.Application.Auth;
using Reservo.Domain.Users;

namespace Reservo.Api.Auth;

/// <summary>Достаёт текущего пользователя из проверенного JWT HTTP-запроса.</summary>
internal sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    private ClaimsPrincipal? Principal => httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated == true;

    public int Id =>
        int.TryParse(Principal?.FindFirstValue(ClaimNames.UserId), NumberStyles.None, CultureInfo.InvariantCulture, out var id)
            ? id
            : throw new InvalidOperationException("В текущем запросе нет вошедшего пользователя.");

    public bool IsAdmin => Principal?.IsInRole(nameof(UserRole.Admin)) == true;
}
