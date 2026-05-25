namespace SiteForce.PaymentApi.Data.Entities;

public enum UploadStatus
{
    Pending,
    Processed,
    Failed,
    PendingConfirmation,
    Cancelled
}

public class AttendanceUpload
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string UploadedBy { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public int RowCount { get; set; }
    public int ErrorCount { get; set; }
    public UploadStatus Status { get; set; } = UploadStatus.Pending;

    public ICollection<AttendanceRecord> Records { get; set; } = new List<AttendanceRecord>();
}
