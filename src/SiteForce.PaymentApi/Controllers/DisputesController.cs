using Microsoft.AspNetCore.Mvc;
using SiteForce.PaymentApi.Data.Entities;
using SiteForce.PaymentApi.DTOs;
using SiteForce.PaymentApi.Services;

namespace SiteForce.PaymentApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DisputesController : ControllerBase
{
    private readonly DisputeService _disputeService;

    public DisputesController(DisputeService disputeService)
    {
        _disputeService = disputeService;
    }

    /// <summary>
    /// Get disputes, optionally filtered by status.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<DisputeDto>>> GetDisputes([FromQuery] string? status = null)
    {
        DisputeStatus? parsedStatus = null;
        if (!string.IsNullOrEmpty(status) && Enum.TryParse<DisputeStatus>(status, true, out var s))
            parsedStatus = s;

        var disputes = await _disputeService.GetDisputesAsync(parsedStatus);

        return Ok(disputes.Select(d => new DisputeDto
        {
            Id = d.Id,
            PaymentLineId = d.PaymentLineId,
            WorkerId = d.PaymentLine.WorkerId,
            SiteName = d.PaymentLine.SiteName,
            RaisedBy = d.RaisedBy,
            Reason = d.Reason.ToString(),
            Description = d.Description,
            Status = d.Status.ToString(),
            CreatedAt = d.CreatedAt,
            ResolvedBy = d.ResolvedBy,
            ResolvedAt = d.ResolvedAt,
            ResolutionNotes = d.ResolutionNotes
        }).ToList());
    }

    /// <summary>
    /// Raise a concern about a payment line (attendance, deduction, or rate issue).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<DisputeDto>> RaiseDispute([FromBody] RaiseDisputeDto request)
    {
        if (!Enum.TryParse<DisputeReason>(request.Reason, true, out var reason))
            return BadRequest($"Invalid reason. Must be one of: {string.Join(", ", Enum.GetNames<DisputeReason>())}");

        if (string.IsNullOrWhiteSpace(request.Description))
            return BadRequest("Description is required");

        var raisedBy = Request.Headers["X-User-Name"].FirstOrDefault() ?? "demo-user";

        try
        {
            var dispute = await _disputeService.RaiseDisputeAsync(
                request.PaymentLineId, raisedBy, reason, request.Description);

            return Ok(new DisputeDto
            {
                Id = dispute.Id,
                PaymentLineId = dispute.PaymentLineId,
                RaisedBy = dispute.RaisedBy,
                Reason = dispute.Reason.ToString(),
                Description = dispute.Description,
                Status = dispute.Status.ToString(),
                CreatedAt = dispute.CreatedAt
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Resolve an open dispute.
    /// </summary>
    [HttpPost("{id}/resolve")]
    public async Task<ActionResult<DisputeDto>> ResolveDispute(int id, [FromBody] ResolveDisputeDto request)
    {
        var resolvedBy = Request.Headers["X-User-Name"].FirstOrDefault() ?? "demo-user";

        try
        {
            var dispute = await _disputeService.ResolveDisputeAsync(id, resolvedBy, request.ResolutionNotes);

            return Ok(new DisputeDto
            {
                Id = dispute.Id,
                PaymentLineId = dispute.PaymentLineId,
                RaisedBy = dispute.RaisedBy,
                Reason = dispute.Reason.ToString(),
                Description = dispute.Description,
                Status = dispute.Status.ToString(),
                CreatedAt = dispute.CreatedAt,
                ResolvedBy = dispute.ResolvedBy,
                ResolvedAt = dispute.ResolvedAt,
                ResolutionNotes = dispute.ResolutionNotes
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
