namespace SiteForce.PaymentApi.DTOs;

public class UploadResultDto
{
    public int UploadId { get; set; }
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int ErrorCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<DuplicateRecordDto> Duplicates { get; set; } = new();
    public bool HasDuplicates => Duplicates.Count > 0;
}

public class DuplicateRecordDto
{
    public string WorkerId { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public int ExistingDaysPresent { get; set; }
    public decimal ExistingDayRate { get; set; }
    public int NewDaysPresent { get; set; }
    public decimal NewDayRate { get; set; }
}

public class ConfirmOverwriteDto
{
    public int UploadId { get; set; }
}

public class CalculateRequestDto
{
    public int UploadId { get; set; }
}

public class PaymentLineDto
{
    public int Id { get; set; }
    public int PaymentRunId { get; set; }
    public string WorkerId { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public decimal GrossAmount { get; set; }
    public decimal Deductions { get; set; }
    public decimal Allowances { get; set; }
    public decimal NetAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string BreakdownJson { get; set; } = "{}";
}

public class BatchDto
{
    public int Id { get; set; }
    public string SiteName { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public int WorkerCount { get; set; }
}

public class RaiseDisputeDto
{
    public int PaymentLineId { get; set; }
    public string Reason { get; set; } = string.Empty;  // "Attendance", "Deduction", "Rate"
    public string Description { get; set; } = string.Empty;
}

public class ResolveDisputeDto
{
    public string ResolutionNotes { get; set; } = string.Empty;
}

public class DisputeDto
{
    public int Id { get; set; }
    public int PaymentLineId { get; set; }
    public string WorkerId { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public string RaisedBy { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string? ResolvedBy { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolutionNotes { get; set; }
}

public class AuditEventDto
{
    public int Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string PayloadJson { get; set; } = "{}";
}

public class SiteRuleConfigDto
{
    public int Id { get; set; }
    public string SiteName { get; set; } = string.Empty;
    public decimal AdvanceDeductionAmount { get; set; }
    public decimal SiteAllowancePercent { get; set; }
    public decimal DisputeThresholdAmount { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string UpdatedBy { get; set; } = string.Empty;
}

public class SaveSiteRuleConfigDto
{
    public string SiteName { get; set; } = string.Empty;
    public decimal AdvanceDeductionAmount { get; set; }
    public decimal SiteAllowancePercent { get; set; }
    public decimal DisputeThresholdAmount { get; set; }
}
