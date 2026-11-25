using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reservo.Api.Auth;
using Reservo.Application.Admin;

namespace Reservo.Api.Controllers;

/// <summary>Дашборд администратора.</summary>
[ApiController]
[Route("api/admin/stats")]
[Authorize(Policy = Policies.Admin)]
[Produces("application/json")]
public sealed class AdminStatsController(IAdminStatsService statsService) : ControllerBase
{
    /// <summary>Сводка дня: брони, что идёт сейчас, очередь заявок, тепловая карта загрузки по часам.</summary>
    /// <remarks>
    /// Ближайшие брони — <c>GET /api/admin/bookings?from=сейчас&amp;status=Confirmed&amp;pageSize=5</c>,
    /// последние события — <c>GET /api/admin/audit?pageSize=5</c>.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<AdminStatsDto>(StatusCodes.Status200OK)]
    public Task<AdminStatsDto> Get([FromQuery] AdminStatsQuery query, CancellationToken cancellationToken) =>
        statsService.GetAsync(query, cancellationToken);
}
