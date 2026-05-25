using Microsoft.EntityFrameworkCore;
using SiteForce.PaymentApi.Data.Entities;

namespace SiteForce.PaymentApi.Data;

public class PaymentDbContext : DbContext
{
    public PaymentDbContext(DbContextOptions<PaymentDbContext> options) : base(options) { }

    public DbSet<AttendanceUpload> AttendanceUploads => Set<AttendanceUpload>();
    public DbSet<AttendanceRecord> AttendanceRecords => Set<AttendanceRecord>();
    public DbSet<PaymentRun> PaymentRuns => Set<PaymentRun>();
    public DbSet<PaymentLine> PaymentLines => Set<PaymentLine>();
    public DbSet<Dispute> Disputes => Set<Dispute>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<SiteRuleConfig> SiteRuleConfigs => Set<SiteRuleConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // AttendanceUpload
        modelBuilder.Entity<AttendanceUpload>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.FileName).HasMaxLength(256).IsRequired();
            e.Property(x => x.UploadedBy).HasMaxLength(100).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        });

        // AttendanceRecord
        modelBuilder.Entity<AttendanceRecord>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.WorkerId).HasMaxLength(50).IsRequired();
            e.Property(x => x.SiteName).HasMaxLength(200).IsRequired();
            e.Property(x => x.DayRate).HasPrecision(18, 2);
            e.Property(x => x.AdvanceTaken).HasPrecision(18, 2);
            e.Property(x => x.Period).HasMaxLength(10).IsRequired();
            e.HasOne(x => x.Upload)
                .WithMany(u => u.Records)
                .HasForeignKey(x => x.UploadId);
        });

        // PaymentRun
        modelBuilder.Entity<PaymentRun>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.SiteName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Period).HasMaxLength(10).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.Property(x => x.ApprovedBy).HasMaxLength(100);
        });

        // PaymentLine
        modelBuilder.Entity<PaymentLine>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.WorkerId).HasMaxLength(50).IsRequired();
            e.Property(x => x.SiteName).HasMaxLength(200).IsRequired();
            e.Property(x => x.GrossAmount).HasPrecision(18, 2);
            e.Property(x => x.Deductions).HasPrecision(18, 2);
            e.Property(x => x.Allowances).HasPrecision(18, 2);
            e.Property(x => x.NetAmount).HasPrecision(18, 2);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.PaymentRun)
                .WithMany(r => r.PaymentLines)
                .HasForeignKey(x => x.PaymentRunId);
        });

        // Dispute
        modelBuilder.Entity<Dispute>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.RaisedBy).HasMaxLength(100).IsRequired();
            e.Property(x => x.Reason).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Description).HasMaxLength(1000).IsRequired();
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ResolvedBy).HasMaxLength(100);
            e.Property(x => x.ResolutionNotes).HasMaxLength(1000);
            e.HasOne(x => x.PaymentLine)
                .WithMany(p => p.Disputes)
                .HasForeignKey(x => x.PaymentLineId);
        });

        // AuditEvent — append-only by convention (enforced at DB level via DENY script)
        modelBuilder.Entity<AuditEvent>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.EventType).HasMaxLength(50).IsRequired();
            e.Property(x => x.EntityType).HasMaxLength(50).IsRequired();
            e.Property(x => x.EntityId).HasMaxLength(50).IsRequired();
            e.Property(x => x.ActorId).HasMaxLength(100).IsRequired();
            e.Property(x => x.ActorName).HasMaxLength(100).IsRequired();
            e.HasIndex(x => new { x.EntityType, x.EntityId });
            e.HasIndex(x => x.Timestamp);
        });

        // SiteRuleConfig
        modelBuilder.Entity<SiteRuleConfig>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.SiteName).HasMaxLength(200).IsRequired();
            e.Property(x => x.AdvanceDeductionAmount).HasPrecision(18, 2);
            e.Property(x => x.SiteAllowancePercent).HasPrecision(18, 2);
            e.Property(x => x.DisputeThresholdAmount).HasPrecision(18, 2);
            e.Property(x => x.UpdatedBy).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.SiteName).IsUnique();
        });
    }
}
