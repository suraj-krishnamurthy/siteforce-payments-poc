using Microsoft.Extensions.Options;
using SiteForce.PaymentApi.Data.Entities;

namespace SiteForce.PaymentApi.Rules;

public class DisputeThresholdRule : IRulePlugin
{
    private readonly decimal _defaultThreshold;

    public string Name => "DisputeThreshold";
    public int Priority => 4;

    public DisputeThresholdRule(IOptions<RuleSettings> settings)
    {
        _defaultThreshold = settings.Value.DisputeThresholdAmount;
    }

    public RuleResult Execute(AttendanceRecord record, CalculationContext context)
    {
        var threshold = context.SiteConfig?.DisputeThresholdAmount ?? _defaultThreshold;

        if (context.NetAmount < threshold)
        {
            context.IsFlaggedDisputed = true;

            return new RuleResult
            {
                RuleName = Name,
                Adjustment = 0,
                AdjustmentType = "flag",
                Description = $"Net pay \u20b9{context.NetAmount:F2} is below threshold \u20b9{threshold} \u2014 flagged for dispute"
            };
        }

        return new RuleResult
        {
            RuleName = Name,
            Adjustment = 0,
            AdjustmentType = "flag",
            Description = $"Net pay \u20b9{context.NetAmount:F2} is above threshold \u20b9{threshold} \u2014 no flag"
        };
    }
}
