using SiteForce.PaymentApi.Data.Entities;

namespace SiteForce.PaymentApi.Rules;

/// <summary>
/// Calculates gross pay: DaysPresent × DayRate
/// </summary>
public class BasePayRule : IRulePlugin
{
    public string Name => "BasePay";
    public int Priority => 1;

    public RuleResult Execute(AttendanceRecord record, CalculationContext context)
    {
        var gross = record.DaysPresent * record.DayRate;
        context.GrossAmount = gross;

        return new RuleResult
        {
            RuleName = Name,
            Adjustment = gross,
            AdjustmentType = "addition",
            Description = $"{record.DaysPresent} days × ₹{record.DayRate}/day = ₹{gross}"
        };
    }
}
