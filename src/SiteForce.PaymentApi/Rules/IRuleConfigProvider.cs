using SiteForce.PaymentApi.Data.Entities;

namespace SiteForce.PaymentApi.Rules;

/// <summary>
/// Abstracts rule configuration resolution per site.
/// Part of the Microkernel extension point — allows swapping config sources
/// (DB, external service, feature flags) without touching the rule engine.
/// </summary>
public interface IRuleConfigProvider
{
    Task<SiteRuleConfig?> GetConfigForSiteAsync(string siteName);
}
