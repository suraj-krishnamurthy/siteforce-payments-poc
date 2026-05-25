namespace SiteForce.PaymentApi.Data.Entities;

public enum PaymentLineStatus
{
    Pending,
    Ready,
    Disputed
}

public class PaymentLine
{
    public int Id { get; set; }
    public int PaymentRunId { get; set; }
    public string WorkerId { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public decimal GrossAmount { get; set; }
    public decimal Deductions { get; set; }
    public decimal Allowances { get; set; }
    public decimal NetAmount { get; set; }
    public PaymentLineStatus Status { get; set; } = PaymentLineStatus.Pending;
    public string BreakdownJson { get; set; } = "{}";

    public PaymentRun PaymentRun { get; set; } = null!;
    public ICollection<Dispute> Disputes { get; set; } = new List<Dispute>();
}
