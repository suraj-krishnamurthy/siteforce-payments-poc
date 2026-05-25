using Microsoft.EntityFrameworkCore;
using SiteForce.PaymentApi.Data;
using SiteForce.PaymentApi.Data.Entities;

namespace SiteForce.PaymentApi.Services;

public class DisputeService
{
    private readonly PaymentDbContext _db;
    private readonly AuditService _audit;

    public DisputeService(PaymentDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<Dispute> RaiseDisputeAsync(int paymentLineId, string raisedBy,
        DisputeReason reason, string description)
    {
        var paymentLine = await _db.PaymentLines.FindAsync(paymentLineId)
            ?? throw new InvalidOperationException($"Payment line {paymentLineId} not found");

        var dispute = new Dispute
        {
            PaymentLineId = paymentLineId,
            RaisedBy = raisedBy,
            Reason = reason,
            Description = description,
            Status = DisputeStatus.Open,
            CreatedAt = DateTime.UtcNow
        };

        _db.Disputes.Add(dispute);

        // Update payment line status
        paymentLine.Status = PaymentLineStatus.Disputed;

        // Update parent payment run status
        var paymentRun = await _db.PaymentRuns.FindAsync(paymentLine.PaymentRunId);
        if (paymentRun != null && paymentRun.Status == PaymentRunStatus.Calculated)
        {
            paymentRun.Status = PaymentRunStatus.Disputed;
        }

        await _db.SaveChangesAsync();

        await _audit.LogAsync(
            "dispute_raised",
            "Dispute",
            dispute.Id.ToString(),
            raisedBy,
            raisedBy,
            new { paymentLineId, reason = reason.ToString(), description }
        );

        return dispute;
    }

    public async Task<Dispute> ResolveDisputeAsync(int disputeId, string resolvedBy, string resolutionNotes)
    {
        var dispute = await _db.Disputes
            .Include(d => d.PaymentLine)
            .FirstOrDefaultAsync(d => d.Id == disputeId)
            ?? throw new InvalidOperationException($"Dispute {disputeId} not found");

        dispute.Status = DisputeStatus.Resolved;
        dispute.ResolvedBy = resolvedBy;
        dispute.ResolvedAt = DateTime.UtcNow;
        dispute.ResolutionNotes = resolutionNotes;

        // Check if all disputes on this payment line are resolved
        var hasOtherOpenDisputes = await _db.Disputes
            .AnyAsync(d => d.PaymentLineId == dispute.PaymentLineId
                && d.Id != disputeId
                && d.Status == DisputeStatus.Open);

        if (!hasOtherOpenDisputes)
        {
            dispute.PaymentLine.Status = PaymentLineStatus.Ready;
        }

        await _db.SaveChangesAsync();

        await _audit.LogAsync(
            "dispute_resolved",
            "Dispute",
            dispute.Id.ToString(),
            resolvedBy,
            resolvedBy,
            new { disputeId, resolutionNotes }
        );

        return dispute;
    }

    public async Task<List<Dispute>> GetDisputesAsync(DisputeStatus? status = null)
    {
        var query = _db.Disputes.Include(d => d.PaymentLine).AsQueryable();

        if (status.HasValue)
            query = query.Where(d => d.Status == status.Value);

        return await query.OrderByDescending(d => d.CreatedAt).ToListAsync();
    }
}
