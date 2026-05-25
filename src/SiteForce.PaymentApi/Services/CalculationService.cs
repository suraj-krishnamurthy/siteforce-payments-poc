using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SiteForce.PaymentApi.Data;
using SiteForce.PaymentApi.Data.Entities;
using SiteForce.PaymentApi.Rules;

namespace SiteForce.PaymentApi.Services;

public class CalculationService
{
    private readonly PaymentDbContext _db;
    private readonly RuleEngine _ruleEngine;
    private readonly IRuleConfigProvider _configProvider;
    private readonly AuditService _audit;

    public CalculationService(
        PaymentDbContext db,
        RuleEngine ruleEngine,
        IRuleConfigProvider configProvider,
        AuditService audit)
    {
        _db = db;
        _ruleEngine = ruleEngine;
        _configProvider = configProvider;
        _audit = audit;
    }

    public async Task<List<PaymentRun>> CalculateForUploadAsync(int uploadId, string actorId)
    {
        var records = await _db.AttendanceRecords
            .Where(r => r.UploadId == uploadId)
            .ToListAsync();

        if (records.Count == 0)
            throw new InvalidOperationException($"No attendance records found for upload {uploadId}");

        // Group by site to create one PaymentRun per site
        var siteGroups = records.GroupBy(r => r.SiteName);
        var paymentRuns = new List<PaymentRun>();

        foreach (var siteGroup in siteGroups)
        {
            var period = siteGroup.First().Period;

            // Fetch per-site rule configuration (falls back to defaults if not found)
            var siteConfig = await _configProvider.GetConfigForSiteAsync(siteGroup.Key);

            var paymentRun = new PaymentRun
            {
                SiteName = siteGroup.Key,
                Period = period,
                Status = PaymentRunStatus.Calculated,
                CreatedAt = DateTime.UtcNow,
                UploadId = uploadId
            };

            _db.PaymentRuns.Add(paymentRun);
            await _db.SaveChangesAsync();

            decimal runTotal = 0;
            bool hasDisputes = false;

            foreach (var record in siteGroup)
            {
                // Execute rule engine (Microkernel core)
                var context = _ruleEngine.Execute(record, siteConfig);

                var paymentLine = new PaymentLine
                {
                    PaymentRunId = paymentRun.Id,
                    WorkerId = record.WorkerId,
                    SiteName = record.SiteName,
                    GrossAmount = context.GrossAmount,
                    Deductions = context.TotalDeductions,
                    Allowances = context.TotalAllowances,
                    NetAmount = context.NetAmount,
                    Status = context.IsFlaggedDisputed ? PaymentLineStatus.Disputed : PaymentLineStatus.Ready,
                    BreakdownJson = JsonSerializer.Serialize(context.AppliedRules)
                };

                if (context.IsFlaggedDisputed)
                    hasDisputes = true;

                runTotal += context.NetAmount;
                _db.PaymentLines.Add(paymentLine);
            }

            paymentRun.TotalAmount = runTotal;
            if (hasDisputes)
                paymentRun.Status = PaymentRunStatus.Disputed;

            await _db.SaveChangesAsync();
            paymentRuns.Add(paymentRun);

            // Audit event per run
            await _audit.LogAsync(
                "payment_calculated",
                "PaymentRun",
                paymentRun.Id.ToString(),
                actorId,
                actorId,
                new { site = siteGroup.Key, period, workerCount = siteGroup.Count(), totalAmount = runTotal }
            );
        }

        return paymentRuns;
    }

    public async Task<PaymentRun> ApproveBatchAsync(int paymentRunId, string approvedBy)
    {
        var run = await _db.PaymentRuns
            .Include(r => r.PaymentLines)
            .FirstOrDefaultAsync(r => r.Id == paymentRunId)
            ?? throw new InvalidOperationException($"Payment run {paymentRunId} not found");

        if (run.Status == PaymentRunStatus.Approved)
            throw new InvalidOperationException("Batch is already approved");

        // Check for payment lines with Disputed status
        var hasDisputedLines = run.PaymentLines.Any(pl => pl.Status == PaymentLineStatus.Disputed);

        if (hasDisputedLines)
            throw new InvalidOperationException("Cannot approve batch with disputed payment lines");

        run.Status = PaymentRunStatus.Approved;
        run.ApprovedBy = approvedBy;
        run.ApprovedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        await _audit.LogAsync(
            "batch_approved",
            "PaymentRun",
            paymentRunId.ToString(),
            approvedBy,
            approvedBy,
            new { site = run.SiteName, period = run.Period, totalAmount = run.TotalAmount }
        );

        return run;
    }
}
