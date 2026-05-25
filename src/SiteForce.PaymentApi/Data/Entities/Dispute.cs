namespace SiteForce.PaymentApi.Data.Entities;

public enum DisputeReason
{
    Attendance,
    Deduction,
    Rate
}

public enum DisputeStatus
{
    Open,
    Resolved
}

public class Dispute
{
    public int Id { get; set; }
    public int PaymentLineId { get; set; }
    public string RaisedBy { get; set; } = string.Empty;
    public DisputeReason Reason { get; set; }
    public string Description { get; set; } = string.Empty;
    public DisputeStatus Status { get; set; } = DisputeStatus.Open;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNotes { get; set; }

    public PaymentLine PaymentLine { get; set; } = null!;
}
