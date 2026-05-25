namespace SiteForce.PaymentApi.Data.Entities;

public class SiteRuleConfig
{
    public int Id { get; set; }
    public string SiteName { get; set; } = string.Empty;
    public decimal AdvanceDeductionAmount { get; set; } = 2000m;
    public decimal SiteAllowancePercent { get; set; } = 10m;
    public decimal DisputeThresholdAmount { get; set; } = 5000m;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string UpdatedBy { get; set; } = string.Empty;
}
