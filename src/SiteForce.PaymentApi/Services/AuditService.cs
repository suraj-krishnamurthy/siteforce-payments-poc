using System.Text.Json;
using SiteForce.PaymentApi.Data;
using SiteForce.PaymentApi.Data.Entities;

namespace SiteForce.PaymentApi.Services;

public class AuditService
{
    private readonly PaymentDbContext _db;

    public AuditService(PaymentDbContext db)
    {
        _db = db;
    }

    public async Task LogAsync(string eventType, string entityType, string entityId,
        string actorId, string actorName, object? payload = null)
    {
        var auditEvent = new AuditEvent
        {
            EventType = eventType,
            EntityType = entityType,
            EntityId = entityId,
            ActorId = actorId,
            ActorName = actorName,
            Timestamp = DateTime.UtcNow,
            PayloadJson = payload != null
                ? JsonSerializer.Serialize(payload)
                : "{}"
        };

        _db.AuditEvents.Add(auditEvent);
        await _db.SaveChangesAsync();
    }

    public async Task<List<AuditEvent>> QueryAsync(string? entityType = null,
        string? entityId = null, DateTime? from = null, DateTime? to = null)
    {
        var query = _db.AuditEvents.AsQueryable();

        if (!string.IsNullOrEmpty(entityType))
            query = query.Where(e => e.EntityType == entityType);

        if (!string.IsNullOrEmpty(entityId))
            query = query.Where(e => e.EntityId == entityId);

        if (from.HasValue)
            query = query.Where(e => e.Timestamp >= from.Value);

        if (to.HasValue)
            query = query.Where(e => e.Timestamp <= to.Value);

        return await Task.FromResult(query.OrderByDescending(e => e.Timestamp).Take(200).ToList());
    }
}
