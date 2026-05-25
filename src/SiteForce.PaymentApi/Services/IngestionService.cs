using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SiteForce.PaymentApi.Data;
using SiteForce.PaymentApi.Data.Entities;
using SiteForce.PaymentApi.DTOs;

namespace SiteForce.PaymentApi.Services;

public class IngestionResult
{
    public int UploadId { get; set; }
    public int TotalRows { get; set; }
    public int ValidRows { get; set; }
    public int ErrorCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<DuplicateRecordDto> Duplicates { get; set; } = new();
}

public class IngestionService
{
    private readonly PaymentDbContext _db;
    private readonly AuditService _audit;

    public IngestionService(PaymentDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<IngestionResult> IngestExcelAsync(Stream fileStream, string fileName, string uploadedBy)
    {
        var result = new IngestionResult();
        var errors = new List<string>();
        var records = new List<AttendanceRecord>();

        using var workbook = new XLWorkbook(fileStream);
        var worksheet = workbook.Worksheets.First();

        var headerRow = worksheet.Row(1);
        var headers = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int col = 1; col <= worksheet.LastColumnUsed()?.ColumnNumber(); col++)
        {
            var headerValue = headerRow.Cell(col).GetString().Trim();
            if (!string.IsNullOrEmpty(headerValue))
                headers[headerValue] = col;
        }

        // Validate required columns
        var requiredColumns = new[] { "WorkerId", "Site", "DaysPresent", "DayRate" };
        foreach (var col in requiredColumns)
        {
            if (!headers.ContainsKey(col))
            {
                errors.Add($"Missing required column: {col}");
            }
        }

        if (errors.Count > 0)
        {
            result.Errors = errors;
            result.ErrorCount = errors.Count;
            return result;
        }

        int workerIdCol = headers["WorkerId"];
        int siteCol = headers["Site"];
        int daysPresentCol = headers["DaysPresent"];
        int dayRateCol = headers["DayRate"];

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        result.TotalRows = lastRow - 1; // exclude header

        // Derive period from current month-end context
        var period = DateTime.UtcNow.ToString("yyyy-MM");

        for (int row = 2; row <= lastRow; row++)
        {
            var workerId = worksheet.Row(row).Cell(workerIdCol).GetString().Trim();
            var site = worksheet.Row(row).Cell(siteCol).GetString().Trim();
            var daysPresentStr = worksheet.Row(row).Cell(daysPresentCol).GetString().Trim();
            var dayRateStr = worksheet.Row(row).Cell(dayRateCol).GetString().Trim();

            // Validate row
            var rowErrors = new List<string>();

            if (string.IsNullOrEmpty(workerId))
                rowErrors.Add("WorkerId is empty");
            if (string.IsNullOrEmpty(site))
                rowErrors.Add("Site is empty");

            if (!int.TryParse(daysPresentStr, out int daysPresent) || daysPresent < 0 || daysPresent > 31)
                rowErrors.Add($"DaysPresent invalid: '{daysPresentStr}'");

            if (!decimal.TryParse(dayRateStr, out decimal dayRate) || dayRate <= 0)
                rowErrors.Add($"DayRate invalid: '{dayRateStr}'");

            if (rowErrors.Count > 0)
            {
                errors.Add($"Row {row}: {string.Join("; ", rowErrors)}");
                continue;
            }

            records.Add(new AttendanceRecord
            {
                WorkerId = workerId,
                SiteName = site,
                DaysPresent = daysPresent,
                DayRate = dayRate,
                Period = period
            });
        }

        // Check for duplicates (same WorkerId + SiteName + Period already in DB)
        var duplicates = new List<DuplicateRecordDto>();
        foreach (var record in records)
        {
            var existing = await _db.AttendanceRecords
                .FirstOrDefaultAsync(r => r.WorkerId == record.WorkerId
                    && r.SiteName == record.SiteName
                    && r.Period == record.Period);

            if (existing != null)
            {
                duplicates.Add(new DuplicateRecordDto
                {
                    WorkerId = record.WorkerId,
                    SiteName = record.SiteName,
                    Period = record.Period,
                    ExistingDaysPresent = existing.DaysPresent,
                    ExistingDayRate = existing.DayRate,
                    NewDaysPresent = record.DaysPresent,
                    NewDayRate = record.DayRate
                });
            }
        }

        // Create upload record
        var upload = new AttendanceUpload
        {
            FileName = fileName,
            UploadedBy = uploadedBy,
            UploadedAt = DateTime.UtcNow,
            RowCount = records.Count,
            ErrorCount = errors.Count,
            Status = duplicates.Count > 0 ? UploadStatus.PendingConfirmation : (errors.Count == 0 ? UploadStatus.Processed : UploadStatus.Processed)
        };

        _db.AttendanceUploads.Add(upload);
        await _db.SaveChangesAsync();

        // Link records to upload
        foreach (var record in records)
        {
            record.UploadId = upload.Id;
        }

        // If duplicates exist, still save the new records (marked with this upload) but don't overwrite yet
        if (duplicates.Count > 0)
        {
            // Save new records temporarily (they will be used during confirm)
            _db.AttendanceRecords.AddRange(records);
            await _db.SaveChangesAsync();

            result.UploadId = upload.Id;
            result.ValidRows = records.Count;
            result.ErrorCount = errors.Count;
            result.Errors = errors;
            result.Duplicates = duplicates;
            return result;
        }

        _db.AttendanceRecords.AddRange(records);
        await _db.SaveChangesAsync();

        // Audit
        await _audit.LogAsync(
            "attendance_uploaded",
            "AttendanceUpload",
            upload.Id.ToString(),
            uploadedBy,
            uploadedBy,
            new { fileName, rowCount = records.Count, errorCount = errors.Count }
        );

        result.UploadId = upload.Id;
        result.ValidRows = records.Count;
        result.ErrorCount = errors.Count;
        result.Errors = errors;
        return result;
    }

    public async Task ConfirmOverwriteAsync(int uploadId, string actorName)
    {
        var upload = await _db.AttendanceUploads.FindAsync(uploadId)
            ?? throw new InvalidOperationException("Upload not found");

        var newRecords = await _db.AttendanceRecords
            .Where(r => r.UploadId == uploadId)
            .ToListAsync();

        foreach (var newRecord in newRecords)
        {
            var existing = await _db.AttendanceRecords
                .FirstOrDefaultAsync(r => r.WorkerId == newRecord.WorkerId
                    && r.SiteName == newRecord.SiteName
                    && r.Period == newRecord.Period
                    && r.UploadId != uploadId);

            if (existing != null)
            {
                // Audit the overwrite
                await _audit.LogAsync(
                    "attendance_overwritten",
                    "AttendanceRecord",
                    existing.Id.ToString(),
                    actorName,
                    actorName,
                    new
                    {
                        workerId = existing.WorkerId,
                        siteName = existing.SiteName,
                        period = existing.Period,
                        oldDaysPresent = existing.DaysPresent,
                        oldDayRate = existing.DayRate,
                        newDaysPresent = newRecord.DaysPresent,
                        newDayRate = newRecord.DayRate
                    }
                );

                // Remove old record
                _db.AttendanceRecords.Remove(existing);
            }
        }

        upload.Status = UploadStatus.Processed;
        await _db.SaveChangesAsync();

        // Audit the confirmation
        await _audit.LogAsync(
            "attendance_overwrite_confirmed",
            "AttendanceUpload",
            uploadId.ToString(),
            actorName,
            actorName,
            new { uploadId, recordCount = newRecords.Count }
        );
    }

    public async Task CancelUploadAsync(int uploadId, string actorName)
    {
        var upload = await _db.AttendanceUploads.FindAsync(uploadId)
            ?? throw new InvalidOperationException("Upload not found");

        // Remove the new records for this upload
        var records = await _db.AttendanceRecords
            .Where(r => r.UploadId == uploadId)
            .ToListAsync();

        _db.AttendanceRecords.RemoveRange(records);
        upload.Status = UploadStatus.Cancelled;
        await _db.SaveChangesAsync();

        await _audit.LogAsync(
            "attendance_upload_cancelled",
            "AttendanceUpload",
            uploadId.ToString(),
            actorName,
            actorName,
            new { uploadId, reason = "User cancelled duplicate overwrite" }
        );
    }
}
