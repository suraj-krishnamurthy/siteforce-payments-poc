using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SiteForce.PaymentApi.Data;
using SiteForce.PaymentApi.Data.Entities;
using SiteForce.PaymentApi.DTOs;
using SiteForce.PaymentApi.Rules;

namespace SiteForce.PaymentApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class RulesController : ControllerBase
{
    private readonly PaymentDbContext _db;
    private readonly RuleSettings _globalSettings;

    public RulesController(PaymentDbContext db, IOptions<RuleSettings> globalSettings)
    {
        _db = db;
        _globalSettings = globalSettings.Value;
    }

    /// <summary>
    /// Get all site rule configurations.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<List<SiteRuleConfigDto>>> GetAll()
    {
        var configs = await _db.SiteRuleConfigs
            .OrderBy(c => c.SiteName)
            .ToListAsync();

        return Ok(configs.Select(c => new SiteRuleConfigDto
        {
            Id = c.Id,
            SiteName = c.SiteName,
            AdvanceDeductionAmount = c.AdvanceDeductionAmount,
            SiteAllowancePercent = c.SiteAllowancePercent,
            DisputeThresholdAmount = c.DisputeThresholdAmount,
            UpdatedAt = c.UpdatedAt,
            UpdatedBy = c.UpdatedBy
        }).ToList());
    }

    /// <summary>
    /// Get rule configuration for a specific site.
    /// </summary>
    [HttpGet("{siteName}")]
    public async Task<ActionResult<SiteRuleConfigDto>> GetBySite(string siteName)
    {
        var config = await _db.SiteRuleConfigs
            .FirstOrDefaultAsync(c => c.SiteName == siteName);

        if (config == null)
            return NotFound($"No configuration found for site '{siteName}'");

        return Ok(new SiteRuleConfigDto
        {
            Id = config.Id,
            SiteName = config.SiteName,
            AdvanceDeductionAmount = config.AdvanceDeductionAmount,
            SiteAllowancePercent = config.SiteAllowancePercent,
            DisputeThresholdAmount = config.DisputeThresholdAmount,
            UpdatedAt = config.UpdatedAt,
            UpdatedBy = config.UpdatedBy
        });
    }

    /// <summary>
    /// Create or update rule configuration for a site.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<SiteRuleConfigDto>> Save([FromBody] SaveSiteRuleConfigDto request)
    {
        if (string.IsNullOrWhiteSpace(request.SiteName))
            return BadRequest("Site name is required");

        var actorName = Request.Headers["X-User-Name"].FirstOrDefault() ?? "demo-user";

        var existing = await _db.SiteRuleConfigs
            .FirstOrDefaultAsync(c => c.SiteName == request.SiteName);

        if (existing != null)
        {
            existing.AdvanceDeductionAmount = request.AdvanceDeductionAmount;
            existing.SiteAllowancePercent = request.SiteAllowancePercent;
            existing.DisputeThresholdAmount = request.DisputeThresholdAmount;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = actorName;
        }
        else
        {
            existing = new SiteRuleConfig
            {
                SiteName = request.SiteName,
                AdvanceDeductionAmount = request.AdvanceDeductionAmount,
                SiteAllowancePercent = request.SiteAllowancePercent,
                DisputeThresholdAmount = request.DisputeThresholdAmount,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = actorName
            };
            _db.SiteRuleConfigs.Add(existing);
        }

        await _db.SaveChangesAsync();

        return Ok(new SiteRuleConfigDto
        {
            Id = existing.Id,
            SiteName = existing.SiteName,
            AdvanceDeductionAmount = existing.AdvanceDeductionAmount,
            SiteAllowancePercent = existing.SiteAllowancePercent,
            DisputeThresholdAmount = existing.DisputeThresholdAmount,
            UpdatedAt = existing.UpdatedAt,
            UpdatedBy = existing.UpdatedBy
        });
    }

    /// <summary>
    /// Delete rule configuration for a site (reverts to global defaults).
    /// </summary>
    [HttpDelete("{siteName}")]
    public async Task<IActionResult> Delete(string siteName)
    {
        var config = await _db.SiteRuleConfigs
            .FirstOrDefaultAsync(c => c.SiteName == siteName);

        if (config == null)
            return NotFound();

        _db.SiteRuleConfigs.Remove(config);
        await _db.SaveChangesAsync();

        return Ok(new { message = $"Configuration for '{siteName}' deleted. Global defaults will apply." });
    }

    /// <summary>
    /// Get global default rule settings.
    /// </summary>
    [HttpGet("defaults")]
    public ActionResult<SiteRuleConfigDto> GetDefaults()
    {
        return Ok(new SiteRuleConfigDto
        {
            Id = 0,
            SiteName = "Global Default",
            AdvanceDeductionAmount = _globalSettings.AdvanceDeductionAmount,
            SiteAllowancePercent = _globalSettings.DefaultSiteAllowancePercent,
            DisputeThresholdAmount = _globalSettings.DisputeThresholdAmount,
            UpdatedAt = DateTime.MinValue,
            UpdatedBy = "system"
        });
    }
}
