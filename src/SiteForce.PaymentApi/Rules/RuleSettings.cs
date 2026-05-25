namespace SiteForce.PaymentApi.Rules;

/// <summary>
/// Configuration for payment calculation rules, bound from appsettings.json "RuleSettings" section.
/// </summary>
public class RuleSettings
{
    public const string SectionName = "RuleSettings";

    /// <summary>Advance recovery amount (₹) deducted per worker per period. Set 0 for no deduction.</summary>
    public decimal AdvanceDeductionAmount { get; set; } = 0m;

    /// <summary>Default site allowance as a percentage of gross pay.</summary>
    public decimal DefaultSiteAllowancePercent { get; set; } = 10m;

    /// <summary>If net pay falls below this amount, the payment line is auto-flagged as disputed.</summary>
    public decimal DisputeThresholdAmount { get; set; } = 20358m;
}
