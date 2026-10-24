using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Reservo.Api.Auth;
using Reservo.Application.Audit;
using Reservo.Application.Common.Paging;

namespace Reservo.Api.Controllers;

/// <summary>Администрирование: журнал операций.</summary>
[ApiController]
[Route("api/admin/audit")]
[Authorize(Policy = Policies.Admin)]
[Produces("application/json")]
public sealed class AdminAuditController(IAuditService auditService) : ControllerBase
{
    /// <summary>Журнал операций всей системы, свежие записи сверху.</summary>
    /// <remarks>
    /// Кто, что, над чем и когда сделал, с подробностями: старые и новые значения, причина.
    /// Записи без автора (<c>actorId</c> пустой) сделала система — фоновая задача.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<PagedResult<AuditLogDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResult<AuditLogDto>> Get([FromQuery] AuditListQuery query, CancellationToken cancellationToken) =>
        auditService.GetLogAsync(query, cancellationToken);
}
