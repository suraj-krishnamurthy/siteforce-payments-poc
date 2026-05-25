namespace SiteForce.PaymentApi.Data.Entities;

public enum PaymentRunStatus
{
    Draft,
    Calculated,
    Disputed,
    Approved
}

public class PaymentRun
{
    public int Id { get; set; }
    public string SiteName { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public PaymentRunStatus Status { get; set; } = PaymentRunStatus.Draft;
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int UploadId { get; set; }

    public ICollection<PaymentLine> PaymentLines { get; set; } = new List<PaymentLine>();
}
