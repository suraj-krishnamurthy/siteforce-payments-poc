using Microsoft.EntityFrameworkCore;
using SiteForce.PaymentApi.Data;
using SiteForce.PaymentApi.Data.Entities;

namespace SiteForce.PaymentApi.Rules;

/// <summary>
/// Database-backed implementation of IRuleConfigProvider.
/// Resolves per-site rule configuration from the SiteRuleConfigs table.
/// </summary>
public class DbRuleConfigProvider : IRuleConfigProvider
{
    private readonly PaymentDbContext _db;

    public DbRuleConfigProvider(PaymentDbContext db)
    {
        _db = db;
    }

    public async Task<SiteRuleConfig?> GetConfigForSiteAsync(string siteName)
    {
        return await _db.SiteRuleConfigs
            .FirstOrDefaultAsync(c => c.SiteName == siteName);
    }
}
