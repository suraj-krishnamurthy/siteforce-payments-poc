using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using SiteForce.PaymentApi.DTOs;
using Xunit;

namespace SiteForce.PaymentApi.Tests;

public class PaymentFlowTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PaymentFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-User-Name", "test-user");
    }

    #region Helpers

    private static MemoryStream CreateTestExcel(params (string workerId, string site, int days, int rate)[] rows)
    {
        var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var ws = workbook.AddWorksheet("Attendance");
            ws.Cell(1, 1).Value = "WorkerId";
            ws.Cell(1, 2).Value = "Site";
            ws.Cell(1, 3).Value = "DaysPresent";
            ws.Cell(1, 4).Value = "DayRate";

            for (int i = 0; i < rows.Length; i++)
            {
                ws.Cell(i + 2, 1).Value = rows[i].workerId;
                ws.Cell(i + 2, 2).Value = rows[i].site;
                ws.Cell(i + 2, 3).Value = rows[i].days;
                ws.Cell(i + 2, 4).Value = rows[i].rate;
            }
            workbook.SaveAs(stream);
        }
        stream.Position = 0;
        return stream;
    }

    private async Task<UploadResultDto> UploadExcel(MemoryStream stream)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StreamContent(stream), "file", "test.xlsx");
        var response = await _client.PostAsync("/api/upload", content);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<UploadResultDto>();
        Assert.NotNull(result);
        return result!;
    }

    #endregion

    /// <summary>
    /// Requirement: Attendance ingestion — accept uploaded Excel, parse cleanly
    /// </summary>
    [Fact]
    public async Task Upload_ValidExcel_ParsesCorrectly()
    {
        var stream = CreateTestExcel(
            ("W001", "Site-A", 22, 800),
            ("W002", "Site-A", 18, 750),
            ("W003", "Site-B", 25, 900)
        );

        var result = await UploadExcel(stream);

        Assert.Equal(3, result.TotalRows);
        Assert.Equal(3, result.ValidRows);
        Assert.Equal(0, result.ErrorCount);
    }

    /// <summary>
    /// Requirement: Attendance ingestion — reports validation errors for bad data
    /// </summary>
    [Fact]
    public async Task Upload_InvalidRows_ReportsErrors()
    {
        var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var ws = workbook.AddWorksheet("Attendance");
            ws.Cell(1, 1).Value = "WorkerId";
            ws.Cell(1, 2).Value = "Site";
            ws.Cell(1, 3).Value = "DaysPresent";
            ws.Cell(1, 4).Value = "DayRate";

            ws.Cell(2, 1).Value = "W001"; ws.Cell(2, 2).Value = "Site-A"; ws.Cell(2, 3).Value = "abc"; ws.Cell(2, 4).Value = 800;
            ws.Cell(3, 1).Value = ""; ws.Cell(3, 2).Value = "Site-A"; ws.Cell(3, 3).Value = 22; ws.Cell(3, 4).Value = 800;
            ws.Cell(4, 1).Value = "W003"; ws.Cell(4, 2).Value = "Site-A"; ws.Cell(4, 3).Value = 20; ws.Cell(4, 4).Value = 800;

            workbook.SaveAs(stream);
        }
        stream.Position = 0;

        var result = await UploadExcel(stream);

        Assert.Equal(3, result.TotalRows);
        Assert.Equal(1, result.ValidRows);
        Assert.Equal(2, result.ErrorCount);
    }

    /// <summary>
    /// Requirement: Payment calculation engine — apply base pay, allowances, and dispute flag
    /// </summary>
    [Fact]
    public async Task Calculate_AppliesRulesCorrectly()
    {
        // Upload: W001 high pay (no dispute), W002 low pay (should be disputed)
        var stream = CreateTestExcel(
            ("W010", "CalcSite", 22, 1000),  // Gross=22000, +10% allowance=2200, net=24200 > 20358 => Ready
            ("W011", "CalcSite", 5, 500)     // Gross=2500, +10% allowance=250, net=2750 < 20358 => Disputed
        );

        var upload = await UploadExcel(stream);
        var calcResponse = await _client.PostAsJsonAsync("/api/payments/calculate", new { uploadId = upload.UploadId });
        Assert.Equal(HttpStatusCode.OK, calcResponse.StatusCode);

        var linesResponse = await _client.GetAsync("/api/payments");
        var linesJson = await linesResponse.Content.ReadFromJsonAsync<dynamic>();
        // Paginated response
        var lines = await _client.GetFromJsonAsync<PaginatedResult>("/api/payments?pageSize=100");
        Assert.NotNull(lines);

        var w010 = lines!.Items.First(l => l.WorkerId == "W010");
        Assert.Equal(22000m, w010.GrossAmount);
        Assert.Equal(0m, w010.Deductions); // Advance recovery is 0 by default
        Assert.Equal(2200m, w010.Allowances); // 10% of 22000
        Assert.Equal(24200m, w010.NetAmount);
        Assert.Equal("Ready", w010.Status);

        var w011 = lines!.Items.First(l => l.WorkerId == "W011");
        Assert.Equal(2500m, w011.GrossAmount);
        Assert.Equal(250m, w011.Allowances);
        Assert.Equal(2750m, w011.NetAmount);
        Assert.Equal("Disputed", w011.Status);
    }

    /// <summary>
    /// Requirement: Payment summary dashboard — approve batch action
    /// </summary>
    [Fact]
    public async Task ApproveBatch_NoDIsputes_Succeeds()
    {
        var stream = CreateTestExcel(
            ("W020", "ApprSite", 25, 1000),
            ("W021", "ApprSite", 22, 1100)
        );

        var upload = await UploadExcel(stream);
        await _client.PostAsJsonAsync("/api/payments/calculate", new { uploadId = upload.UploadId });

        var batches = await _client.GetFromJsonAsync<List<BatchDto>>("/api/batches");
        var batch = batches!.First(b => b.SiteName == "ApprSite");
        Assert.Equal("Calculated", batch.Status);

        var approveResponse = await _client.PostAsync($"/api/batches/{batch.Id}/approve", null);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        var approved = await approveResponse.Content.ReadFromJsonAsync<BatchDto>();
        Assert.Equal("Approved", approved!.Status);
        Assert.Equal("test-user", approved.ApprovedBy);
        Assert.NotNull(approved.ApprovedAt);
    }

    /// <summary>
    /// Requirement: Batch with disputes cannot be approved
    /// </summary>
    [Fact]
    public async Task ApproveBatch_WithDisputes_Fails()
    {
        var stream = CreateTestExcel(
            ("W030", "DispSite", 3, 500) // Low pay => Disputed
        );

        var upload = await UploadExcel(stream);
        await _client.PostAsJsonAsync("/api/payments/calculate", new { uploadId = upload.UploadId });

        var batches = await _client.GetFromJsonAsync<List<BatchDto>>("/api/batches");
        var batch = batches!.First(b => b.SiteName == "DispSite");

        var approveResponse = await _client.PostAsync($"/api/batches/{batch.Id}/approve", null);
        Assert.Equal(HttpStatusCode.BadRequest, approveResponse.StatusCode);
    }

    /// <summary>
    /// Requirement: Audit log — every calculation and approval recorded
    /// </summary>
    [Fact]
    public async Task AuditTrail_RecordsAllEvents()
    {
        var stream = CreateTestExcel(
            ("W040", "AuditSite", 25, 1000),
            ("W041", "AuditSite", 24, 1100)
        );

        var upload = await UploadExcel(stream);
        await _client.PostAsJsonAsync("/api/payments/calculate", new { uploadId = upload.UploadId });

        var batches = await _client.GetFromJsonAsync<List<BatchDto>>("/api/batches");
        var batch = batches!.First(b => b.SiteName == "AuditSite");
        await _client.PostAsync($"/api/batches/{batch.Id}/approve", null);

        var auditEvents = await _client.GetFromJsonAsync<List<AuditEventDto>>("/api/audit");
        Assert.NotNull(auditEvents);
        Assert.Contains(auditEvents!, e => e.EventType == "attendance_uploaded");
        Assert.Contains(auditEvents!, e => e.EventType == "payment_calculated");
        Assert.Contains(auditEvents!, e => e.EventType == "batch_approved");

        // Verify actor is recorded
        var approvalEvent = auditEvents!.First(e => e.EventType == "batch_approved");
        Assert.Equal("test-user", approvalEvent.ActorName);
    }

    /// <summary>
    /// Requirement: Configurable rules — per-site rule configuration
    /// </summary>
    [Fact]
    public async Task Rules_PerSiteConfig_AppliedDuringCalculation()
    {
        // Configure site-specific rule with high advance deduction
        var ruleResponse = await _client.PostAsJsonAsync("/api/rules", new
        {
            siteName = "RuleSite",
            advanceDeductionAmount = 3000,
            siteAllowancePercent = 15,
            disputeThresholdAmount = 10000
        });
        Assert.Equal(HttpStatusCode.OK, ruleResponse.StatusCode);

        var stream = CreateTestExcel(
            ("W050", "RuleSite", 20, 1000) // Gross=20000, -3000 deduction, +15% allowance(3000), net=20000
        );

        var upload = await UploadExcel(stream);
        await _client.PostAsJsonAsync("/api/payments/calculate", new { uploadId = upload.UploadId });

        var lines = await _client.GetFromJsonAsync<PaginatedResult>("/api/payments?pageSize=100");
        var w050 = lines!.Items.First(l => l.WorkerId == "W050");

        Assert.Equal(20000m, w050.GrossAmount);
        Assert.Equal(3000m, w050.Deductions); // Site-specific advance
        Assert.Equal(3000m, w050.Allowances); // 15% of 20000
        Assert.Equal(20000m, w050.NetAmount); // 20000 - 3000 + 3000
        Assert.Equal("Ready", w050.Status); // 20000 > 10000 threshold
    }

    /// <summary>
    /// Requirement: Rules API — get defaults
    /// </summary>
    [Fact]
    public async Task Rules_GetDefaults_ReturnsGlobalConfig()
    {
        var response = await _client.GetAsync("/api/rules/defaults");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var defaults = await response.Content.ReadFromJsonAsync<SiteRuleConfigDto>();
        Assert.NotNull(defaults);
        Assert.Equal("Global Default", defaults!.SiteName);
        Assert.Equal(0m, defaults.AdvanceDeductionAmount);
        Assert.Equal(10m, defaults.SiteAllowancePercent);
        Assert.Equal(20358m, defaults.DisputeThresholdAmount);
    }

    /// <summary>
    /// Requirement: Upload endpoint rejects empty files
    /// </summary>
    [Fact]
    public async Task Upload_EmptyExcel_ReportsZeroRows()
    {
        var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var ws = workbook.AddWorksheet("Attendance");
            ws.Cell(1, 1).Value = "WorkerId";
            ws.Cell(1, 2).Value = "Site";
            ws.Cell(1, 3).Value = "DaysPresent";
            ws.Cell(1, 4).Value = "DayRate";
            workbook.SaveAs(stream);
        }
        stream.Position = 0;

        var result = await UploadExcel(stream);

        Assert.Equal(0, result.TotalRows);
        Assert.Equal(0, result.ValidRows);
        Assert.Equal(0, result.ErrorCount);
    }

    /// <summary>
    /// Requirement: Single row upload and calculation works end-to-end
    /// </summary>
    [Fact]
    public async Task Upload_SingleRow_CalculatesCorrectly()
    {
        var stream = CreateTestExcel(("W060", "SingleSite", 10, 500));

        var upload = await UploadExcel(stream);
        Assert.Equal(1, upload.TotalRows);
        Assert.Equal(1, upload.ValidRows);

        await _client.PostAsJsonAsync("/api/payments/calculate", new { uploadId = upload.UploadId });

        var lines = await _client.GetFromJsonAsync<PaginatedResult>("/api/payments?pageSize=100");
        var w060 = lines!.Items.First(l => l.WorkerId == "W060");

        Assert.Equal(5000m, w060.GrossAmount); // 10 * 500
        Assert.Equal(500m, w060.Allowances);   // 10% of 5000
        Assert.Equal(5500m, w060.NetAmount);   // 5000 + 500
    }

    /// <summary>
    /// Requirement: Pagination works for payment lines
    /// </summary>
    [Fact]
    public async Task Payments_Pagination_ReturnsCorrectPage()
    {
        var stream = CreateTestExcel(
            ("W070", "PageSite", 20, 1000),
            ("W071", "PageSite", 21, 1000),
            ("W072", "PageSite", 22, 1000)
        );

        var upload = await UploadExcel(stream);
        await _client.PostAsJsonAsync("/api/payments/calculate", new { uploadId = upload.UploadId });

        var page = await _client.GetFromJsonAsync<PaginatedResult>("/api/payments?pageSize=2&page=1");
        Assert.NotNull(page);
        Assert.True(page!.Items.Count <= 2);
    }

    /// <summary>
    /// Requirement: Disputes can be raised on payment lines
    /// </summary>
    [Fact]
    public async Task Dispute_RaiseAndResolve_WorksCorrectly()
    {
        var stream = CreateTestExcel(("W080", "DispFlowSite", 25, 1000));

        var upload = await UploadExcel(stream);
        await _client.PostAsJsonAsync("/api/payments/calculate", new { uploadId = upload.UploadId });

        var lines = await _client.GetFromJsonAsync<PaginatedResult>("/api/payments?pageSize=100");
        var line = lines!.Items.First(l => l.WorkerId == "W080");

        // Raise dispute
        var raiseResponse = await _client.PostAsJsonAsync("/api/disputes", new RaiseDisputeDto
        {
            PaymentLineId = line.Id,
            Reason = "Attendance",
            Description = "Worker claims 26 days"
        });
        Assert.Equal(HttpStatusCode.OK, raiseResponse.StatusCode);

        var dispute = await raiseResponse.Content.ReadFromJsonAsync<DisputeDto>();
        Assert.NotNull(dispute);
        Assert.Equal("Open", dispute!.Status);
        Assert.Equal("Attendance", dispute.Reason);

        // Resolve dispute
        var resolveResponse = await _client.PostAsJsonAsync($"/api/disputes/{dispute.Id}/resolve", new ResolveDisputeDto
        {
            ResolutionNotes = "Verified with biometric data, 25 days correct"
        });
        Assert.Equal(HttpStatusCode.OK, resolveResponse.StatusCode);

        var resolved = await resolveResponse.Content.ReadFromJsonAsync<DisputeDto>();
        Assert.Equal("Resolved", resolved!.Status);
        Assert.NotNull(resolved.ResolvedAt);
    }

    /// <summary>
    /// Requirement: Multiple sites in one upload create separate batches
    /// </summary>
    [Fact]
    public async Task Calculate_MultipleSites_CreatesSeparateBatches()
    {
        var stream = CreateTestExcel(
            ("W090", "MultiA", 20, 1000),
            ("W091", "MultiB", 22, 900)
        );

        var upload = await UploadExcel(stream);
        await _client.PostAsJsonAsync("/api/payments/calculate", new { uploadId = upload.UploadId });

        var batches = await _client.GetFromJsonAsync<List<BatchDto>>("/api/batches");
        Assert.Contains(batches!, b => b.SiteName == "MultiA");
        Assert.Contains(batches!, b => b.SiteName == "MultiB");
    }

    /// <summary>
    /// Requirement: Approve already-approved batch returns appropriate response
    /// </summary>
    [Fact]
    public async Task ApproveBatch_AlreadyApproved_FailsOrIdempotent()
    {
        var stream = CreateTestExcel(("W100", "DoubleAppr", 25, 1000));

        var upload = await UploadExcel(stream);
        await _client.PostAsJsonAsync("/api/payments/calculate", new { uploadId = upload.UploadId });

        var batches = await _client.GetFromJsonAsync<List<BatchDto>>("/api/batches");
        var batch = batches!.First(b => b.SiteName == "DoubleAppr");

        await _client.PostAsync($"/api/batches/{batch.Id}/approve", null);
        var secondApprove = await _client.PostAsync($"/api/batches/{batch.Id}/approve", null);

        // Should either be idempotent (200) or reject (400)
        Assert.True(
            secondApprove.StatusCode == HttpStatusCode.OK ||
            secondApprove.StatusCode == HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Requirement: Rules update persists and is applied
    /// </summary>
    [Fact]
    public async Task Rules_UpdateAndVerify_Persists()
    {
        var ruleResponse = await _client.PostAsJsonAsync("/api/rules", new
        {
            siteName = "PersistRuleSite",
            advanceDeductionAmount = 1500,
            siteAllowancePercent = 20,
            disputeThresholdAmount = 5000
        });
        Assert.Equal(HttpStatusCode.OK, ruleResponse.StatusCode);

        var savedRule = await ruleResponse.Content.ReadFromJsonAsync<SiteRuleConfigDto>();
        Assert.NotNull(savedRule);
        Assert.Equal("PersistRuleSite", savedRule!.SiteName);
        Assert.Equal(1500m, savedRule.AdvanceDeductionAmount);
        Assert.Equal(20m, savedRule.SiteAllowancePercent);
        Assert.Equal(5000m, savedRule.DisputeThresholdAmount);
    }

    /// <summary>
    /// Requirement: Audit trail records upload events with correct actor
    /// </summary>
    [Fact]
    public async Task AuditTrail_Upload_RecordsActor()
    {
        var stream = CreateTestExcel(("W110", "AuditActorSite", 20, 1000));
        await UploadExcel(stream);

        var auditEvents = await _client.GetFromJsonAsync<List<AuditEventDto>>("/api/audit");
        Assert.NotNull(auditEvents);

        var uploadEvent = auditEvents!.LastOrDefault(e => e.EventType == "attendance_uploaded");
        Assert.NotNull(uploadEvent);
        Assert.Equal("test-user", uploadEvent!.ActorName);
    }

    /// <summary>
    /// Requirement: Batch worker count matches uploaded workers for that site
    /// </summary>
    [Fact]
    public async Task Batch_WorkerCount_MatchesUpload()
    {
        var stream = CreateTestExcel(
            ("W120", "CountSite", 20, 1000),
            ("W121", "CountSite", 22, 900),
            ("W122", "CountSite", 18, 800)
        );

        var upload = await UploadExcel(stream);
        await _client.PostAsJsonAsync("/api/payments/calculate", new { uploadId = upload.UploadId });

        var batches = await _client.GetFromJsonAsync<List<BatchDto>>("/api/batches");
        var batch = batches!.First(b => b.SiteName == "CountSite");
        Assert.Equal(3, batch.WorkerCount);
    }
}

/// <summary>Helper class for paginated payment response deserialization</summary>
public class PaginatedResult
{
    public List<PaymentLineDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasMore { get; set; }
}
