namespace SiteForce.PaymentApi.Data.Entities;

public class AttendanceRecord
{
    public int Id { get; set; }
    public int UploadId { get; set; }
    public string WorkerId { get; set; } = string.Empty;
    public string SiteName { get; set; } = string.Empty;
    public int DaysPresent { get; set; }
    public decimal DayRate { get; set; }
    public decimal AdvanceTaken { get; set; } // kept for DB compatibility, defaults to 0
    public string Period { get; set; } = string.Empty; // e.g. "2026-05"

    public AttendanceUpload Upload { get; set; } = null!;
}
