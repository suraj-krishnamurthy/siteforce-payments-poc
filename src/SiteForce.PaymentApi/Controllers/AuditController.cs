using Microsoft.AspNetCore.Mvc;
using SiteForce.PaymentApi.DTOs;
using SiteForce.PaymentApi.Services;

namespace SiteForce.PaymentApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuditController : ControllerBase
{
    private readonly AuditService _auditService;

    public AuditController(AuditService auditService)
    {
        _auditService = auditService;
    }

    /// <summary>
    /// Query audit events with optional filters.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<AuditEventDto>>> GetAuditEvents(
        [FromQuery] string? entityType = null,
        [FromQuery] string? entityId = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var events = await _auditService.QueryAsync(entityType, entityId, from, to);

        return Ok(events.Select(e => new AuditEventDto
        {
            Id = e.Id,
            EventType = e.EventType,
            EntityType = e.EntityType,
            EntityId = e.EntityId,
            ActorId = e.ActorId,
            ActorName = e.ActorName,
            Timestamp = e.Timestamp,
            PayloadJson = e.PayloadJson
        }).ToList());
    }
}
