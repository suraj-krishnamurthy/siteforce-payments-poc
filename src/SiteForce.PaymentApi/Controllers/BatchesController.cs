using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SiteForce.PaymentApi.Data;
using SiteForce.PaymentApi.DTOs;
using SiteForce.PaymentApi.Services;

namespace SiteForce.PaymentApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class BatchesController : ControllerBase
{
    private readonly CalculationService _calculationService;
    private readonly PaymentDbContext _db;

    public BatchesController(CalculationService calculationService, PaymentDbContext db)
    {
        _calculationService = calculationService;
        _db = db;
    }

    /// <summary>
    /// Get all payment batches (payment runs) with status and worker count.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<BatchDto>>> GetBatches()
    {
        var runs = await _db.PaymentRuns
            .Include(r => r.PaymentLines)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return Ok(runs.Select(r => new BatchDto
        {
            Id = r.Id,
            SiteName = r.SiteName,
            Period = r.Period,
            Status = r.Status.ToString(),
            TotalAmount = r.TotalAmount,
            CreatedAt = r.CreatedAt,
            ApprovedBy = r.ApprovedBy,
            ApprovedAt = r.ApprovedAt,
            WorkerCount = r.PaymentLines.Count
        }).ToList());
    }

    /// <summary>
    /// One-click batch approval. Approves the entire payment run for a site.
    /// </summary>
    [HttpPost("{id}/approve")]
    public async Task<ActionResult<BatchDto>> ApproveBatch(int id)
    {
        var approvedBy = Request.Headers["X-User-Name"].FirstOrDefault() ?? "demo-user";

        try
        {
            var run = await _calculationService.ApproveBatchAsync(id, approvedBy);

            return Ok(new BatchDto
            {
                Id = run.Id,
                SiteName = run.SiteName,
                Period = run.Period,
                Status = run.Status.ToString(),
                TotalAmount = run.TotalAmount,
                CreatedAt = run.CreatedAt,
                ApprovedBy = run.ApprovedBy,
                ApprovedAt = run.ApprovedAt,
                WorkerCount = run.PaymentLines.Count
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
