using SiteForce.PaymentApi.Data.Entities;

namespace SiteForce.PaymentApi.Rules;

public class CalculationContext
{
    public decimal GrossAmount { get; set; }
    public decimal TotalDeductions { get; set; }
    public decimal TotalAllowances { get; set; }
    public decimal NetAmount => GrossAmount - TotalDeductions + TotalAllowances;
    public List<RuleResult> AppliedRules { get; set; } = new();
    public bool IsFlaggedDisputed { get; set; }
    public SiteRuleConfig? SiteConfig { get; set; }
}

public class RuleResult
{
    public string RuleName { get; set; } = string.Empty;
    public decimal Adjustment { get; set; }
    public string AdjustmentType { get; set; } = string.Empty; // "addition", "deduction", "flag"
    public string Description { get; set; } = string.Empty;
}

public interface IRulePlugin
{
    string Name { get; }
    int Priority { get; }
    RuleResult Execute(AttendanceRecord record, CalculationContext context);
}
