using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SiteForce.PaymentApi.Data;
using SiteForce.PaymentApi.Data.Entities;
using SiteForce.PaymentApi.DTOs;
using SiteForce.PaymentApi.Services;

namespace SiteForce.PaymentApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly CalculationService _calculationService;
    private readonly PaymentDbContext _db;

    public PaymentsController(CalculationService calculationService, PaymentDbContext db)
    {
        _calculationService = calculationService;
        _db = db;
    }

    /// <summary>
    /// Trigger payment calculation for a given upload.
    /// </summary>
    [HttpPost("calculate")]
    public async Task<ActionResult<List<BatchDto>>> Calculate([FromBody] CalculateRequestDto request)
    {
        var actorId = Request.Headers["X-User-Name"].FirstOrDefault() ?? "demo-user";

        try
        {
            var runs = await _calculationService.CalculateForUploadAsync(request.UploadId, actorId);

            var result = runs.Select(r => new BatchDto
            {
                Id = r.Id,
                SiteName = r.SiteName,
                Period = r.Period,
                Status = r.Status.ToString(),
                TotalAmount = r.TotalAmount,
                CreatedAt = r.CreatedAt,
                WorkerCount = r.PaymentLines.Count
            }).ToList();

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Get payment lines with optional filtering by site and status, with pagination.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult> GetPaymentLines(
        [FromQuery] string? siteName = null,
        [FromQuery] string? status = null,
        [FromQuery] int? paymentRunId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = _db.PaymentLines.AsQueryable();

        if (!string.IsNullOrEmpty(siteName))
            query = query.Where(p => p.SiteName == siteName);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<PaymentLineStatus>(status, true, out var parsedStatus))
            query = query.Where(p => p.Status == parsedStatus);

        if (paymentRunId.HasValue)
            query = query.Where(p => p.PaymentRunId == paymentRunId.Value);

        var totalCount = await query.CountAsync();

        var lines = await query
            .OrderBy(p => p.SiteName).ThenBy(p => p.WorkerId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var items = lines.Select(l => new PaymentLineDto
        {
            Id = l.Id,
            PaymentRunId = l.PaymentRunId,
            WorkerId = l.WorkerId,
            SiteName = l.SiteName,
            GrossAmount = l.GrossAmount,
            Deductions = l.Deductions,
            Allowances = l.Allowances,
            NetAmount = l.NetAmount,
            Status = l.Status.ToString(),
            BreakdownJson = l.BreakdownJson
        }).ToList();

        return Ok(new { items, totalCount, page, pageSize, hasMore = page * pageSize < totalCount });
    }
}
