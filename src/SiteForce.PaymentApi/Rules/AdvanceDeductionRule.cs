using Microsoft.Extensions.Options;
using SiteForce.PaymentApi.Data.Entities;

namespace SiteForce.PaymentApi.Rules;

/// <summary>
/// Deducts advance recovery amount from the worker's pay.
/// Amount is configured per site or uses global default.
/// </summary>
public class AdvanceDeductionRule : IRulePlugin
{
    private readonly decimal _defaultAdvanceAmount;

    public string Name => "AdvanceRecovery";
    public int Priority => 2;

    public AdvanceDeductionRule(IOptions<RuleSettings> settings)
    {
        _defaultAdvanceAmount = settings.Value.AdvanceDeductionAmount;
    }

    public RuleResult Execute(AttendanceRecord record, CalculationContext context)
    {
        var deduction = context.SiteConfig?.AdvanceDeductionAmount ?? _defaultAdvanceAmount;

        if (deduction == 0)
        {
            return new RuleResult
            {
                RuleName = Name,
                Adjustment = 0,
                AdjustmentType = "deduction",
                Description = "No advance recovery this period"
            };
        }

        context.TotalDeductions += deduction;

        return new RuleResult
        {
            RuleName = Name,
            Adjustment = deduction,
            AdjustmentType = "deduction",
            Description = $"Advance recovery: \u20b9{deduction}"
        };
    }
}
