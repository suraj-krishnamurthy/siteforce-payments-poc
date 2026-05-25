namespace SiteForce.PaymentApi.Data.Entities;

public class AuditEvent
{
    public int Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string ActorId { get; set; } = string.Empty;
    public string ActorName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string PayloadJson { get; set; } = "{}";
}
