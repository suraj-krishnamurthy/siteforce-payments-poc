using Microsoft.Extensions.Options;
using SiteForce.PaymentApi.Data.Entities;

namespace SiteForce.PaymentApi.Rules;

public class SiteAllowanceRule : IRulePlugin
{
    private readonly decimal _defaultAllowancePercent;

    public string Name => "SiteAllowance";
    public int Priority => 3;

    public SiteAllowanceRule(IOptions<RuleSettings> settings)
    {
        _defaultAllowancePercent = settings.Value.DefaultSiteAllowancePercent;
    }

    public RuleResult Execute(AttendanceRecord record, CalculationContext context)
    {
        var allowancePercent = context.SiteConfig?.SiteAllowancePercent ?? _defaultAllowancePercent;
        var allowance = context.GrossAmount * (allowancePercent / 100m);
        context.TotalAllowances += allowance;

        return new RuleResult
        {
            RuleName = Name,
            Adjustment = allowance,
            AdjustmentType = "addition",
            Description = $"Site allowance ({allowancePercent}% of \u20b9{context.GrossAmount}): \u20b9{allowance:F2}"
        };
    }
}
