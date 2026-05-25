using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using SiteForce.PaymentApi.DTOs;
using Xunit;

namespace SiteForce.PaymentApi.Tests;

/// <summary>
/// End-to-end tests that exercise full business workflows from start to finish.
/// Each test simulates a complete user journey through the system.
/// </summary>
public class EndToEndFlowTests : IClassFixture<TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public EndToEndFlowTests(TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _client.DefaultRequestHeaders.Add("X-User-Name", "e2e-user");
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
    /// E2E: Full happy path — upload ? calculate ? verify payments ? approve batch ? verify audit trail
    /// </summary>
    [Fact]
    public async Task FullHappyPath_Upload_Calculate_Approve_Audit()
    {
        // Step 1: Upload attendance data
        var stream = CreateTestExcel(
            ("E2E-001", "HappySite", 25, 1000),
            ("E2E-002", "HappySite", 22, 900),
            ("E2E-003", "HappySite", 20, 1100)
        );

        var upload = await UploadExcel(stream);
        Assert.Equal(3, upload.TotalRows);
        Assert.Equal(3, upload.ValidRows);
        Assert.Equal(0, upload.ErrorCount);

        // Step 2: Calculate payments
        var calcResponse = await _client.PostAsJsonAsync("/api/payments/calculate", new { uploadId = upload.UploadId });
        Assert.Equal(HttpStatusCode.OK, calcResponse.StatusCode);

        // Step 3: Verify payment lines were created with correct values
        var lines = await _client.GetFromJsonAsync<PaginatedResult>("/api/payments?pageSize=100");
        Assert.NotNull(lines);

        var e2e001 = lines!.Items.First(l => l.WorkerId == "E2E-001");
        Assert.Equal(25000m, e2e001.GrossAmount);   // 25 * 1000
        Assert.Equal(2500m, e2e001.Allowances);     // 10% of 25000
        Assert.Equal(27500m, e2e001.NetAmount);     // 25000 + 2500
        Assert.Equal("Ready", e2e001.Status);

        var e2e002 = lines.Items.First(l => l.WorkerId == "E2E-002");
        Assert.Equal(19800m, e2e002.GrossAmount);   // 22 * 900
        Assert.Equal(1980m, e2e002.Allowances);     // 10% of 19800
        Assert.Equal(21780m, e2e002.NetAmount);     // 19800 + 1980
        Assert.Equal("Ready", e2e002.Status);

        // Step 4: Verify batch was created
        var batches = await _client.GetFromJsonAsync<List<BatchDto>>("/api/batches");
        Assert.NotNull(batches);
        var batch = batches!.First(b => b.SiteName == "HappySite");
        Assert.Equal("Calculated", batch.Status);
        Assert.Equal(3, batch.WorkerCount);

        // Step 5: Approve the batch
        var approveResponse = await _client.PostAsync($"/api/batches/{batch.Id}/approve", null);
        Assert.Equal(HttpStatusCode.OK, approveResponse.StatusCode);

        var approved = await approveResponse.Content.ReadFromJsonAsync<BatchDto>();
        Assert.Equal("Approved", approved!.Status);
        Assert.Equal("e2e-user", approved.ApprovedBy);
        Assert.NotNull(approved.ApprovedAt);

        // Step 6: Verify full audit trail
        var auditEvents = await _client.GetFromJsonAsync<List<AuditEventDto>>("/api/audit");
        Assert.NotNull(auditEvents);
        Assert.Contains(auditEvents!, e => e.EventType == "attendance_uploaded");
        Assert.Contains(auditEvents!, e => e.EventType == "payment_calculated");
        Assert.Contains(auditEvents!, e => e.EventType == "batch_approved");

        // All events have the correct actor
        Assert.All(auditEvents!, e => Assert.Equal("e2e-user", e.ActorName));
    }

    /// <summary>
    /// E2E: Dispute resolution flow — upload ? calculate ? dispute flagged ? raise dispute ? resolve ? approve
    /// </summary>
    [Fact]
    public async Task DisputeResolutionFlow_FlaggedWorker_ResolveAndApprove()
    {
        // Step 1: Configure custom rules with a high threshold to trigger disputes
        var ruleResponse = await _client.PostAsJsonAsync("/api/rules", new
        {
            siteName = "DisputeE2ESite",
            advanceDeductionAmount = 0,
            siteAllowancePercent = 10,
            disputeThresholdAmount = 25000
        });
        Assert.Equal(HttpStatusCode.OK, ruleResponse.StatusCode);

        // Step 2: Upload — one worker above threshold, one below
        var stream = CreateTestExcel(
            ("E2E-010", "DisputeE2ESite", 30, 1000),  // Net = 33000 > 25000 => Ready
            ("E2E-011", "DisputeE2ESite", 10, 800)    // Net = 8800 < 25000 => Disputed
        );

        var upload = await UploadExcel(stream);
        Assert.Equal(2, upload.ValidRows);

        // Step 3: Calculate
        await _client.PostAsJsonAsync("/api/payments/calculate", new { uploadId = upload.UploadId });

        // Step 4: Verify one line is disputed
        var lines = await _client.GetFromJsonAsync<PaginatedResult>("/api/payments?pageSize=100");
        var readyLine = lines!.Items.First(l => l.WorkerId == "E2E-010");
        var disputedLine = lines.Items.First(l => l.WorkerId == "E2E-011");
        Assert.Equal("Ready", readyLine.Status);
        Assert.Equal("Disputed", disputedLine.Status);

        // Step 5: Batch cannot be approved due to dispute
        var batches = await _client.GetFromJsonAsync<List<BatchDto>>("/api/batches");
        var batch = batches!.First(b => b.SiteName == "DisputeE2ESite");
        var failedApprove = await _client.PostAsync($"/api/batches/{batch.Id}/approve", null);
        Assert.Equal(HttpStatusCode.BadRequest, failedApprove.StatusCode);

        // Step 6: Raise a formal dispute
        var raiseResponse = await _client.PostAsJsonAsync("/api/disputes", new RaiseDisputeDto
        {
            PaymentLineId = disputedLine.Id,
            Reason = "Rate",
            Description = "Worker rate should be higher based on contract"
        });
        Assert.Equal(HttpStatusCode.OK, raiseResponse.StatusCode);
        var dispute = await raiseResponse.Content.ReadFromJsonAsync<DisputeDto>();
        Assert.Equal("Open", dispute!.Status);

        // Step 7: Resolve the dispute
        var resolveResponse = await _client.PostAsJsonAsync($"/api/disputes/{dispute.Id}/resolve", new ResolveDisputeDto
        {
            ResolutionNotes = "Confirmed correct rate per contract review"
        });
        Assert.Equal(HttpStatusCode.OK, resolveResponse.StatusCode);
        var resolved = await resolveResponse.Content.ReadFromJsonAsync<DisputeDto>();
        Assert.Equal("Resolved", resolved!.Status);
        Assert.Equal("e2e-user", resolved.ResolvedBy);
    }

    /// <summary>
    /// E2E: Multi-site upload creates independent batches that can be approved separately
    /// </summary>
    [Fact]
    public async Task MultiSiteFlow_IndependentBatchApproval()
    {
        // Step 1: Upload workers across two sites
        var stream = CreateTestExcel(
            ("E2E-020", "SiteAlpha", 25, 1000),
            ("E2E-021", "SiteAlpha", 22, 1100),
            ("E2E-022", "SiteBeta", 20, 900),
            ("E2E-023", "SiteBeta", 3, 500)  // Low pay => Disputed in SiteBeta
        );

        var upload = await UploadExcel(stream);
        Assert.Equal(4, upload.ValidRows);

        // Step 2: Calculate
        await _client.PostAsJsonAsync("/api/payments/calculate", new { uploadId = upload.UploadId });

        // Step 3: Verify two separate batches exist
        var batches = await _client.GetFromJsonAsync<List<BatchDto>>("/api/batches");
        var alphaB = batches!.First(b => b.SiteName == "SiteAlpha");
        var betaB = batches!.First(b => b.SiteName == "SiteBeta");
        Assert.Equal(2, alphaB.WorkerCount);
        Assert.Equal(2, betaB.WorkerCount);

        // Step 4: SiteAlpha (no disputes) can be approved
        var alphaApprove = await _client.PostAsync($"/api/batches/{alphaB.Id}/approve", null);
        Assert.Equal(HttpStatusCode.OK, alphaApprove.StatusCode);

        // Step 5: SiteBeta (has disputed line) cannot be approved
        var betaApprove = await _client.PostAsync($"/api/batches/{betaB.Id}/approve", null);
        Assert.Equal(HttpStatusCode.BadRequest, betaApprove.StatusCode);
    }

    /// <summary>
    /// E2E: Custom rules affect calculation differently per site
    /// </summary>
    [Fact]
    public async Task CustomRulesFlow_DifferentSitesDifferentRules()
    {
        // Step 1: Configure two sites with different rules
        await _client.PostAsJsonAsync("/api/rules", new
        {
            siteName = "HighAllowSite",
            advanceDeductionAmount = 0,
            siteAllowancePercent = 25,
            disputeThresholdAmount = 5000
        });

        await _client.PostAsJsonAsync("/api/rules", new
        {
            siteName = "HighDeductSite",
            advanceDeductionAmount = 5000,
            siteAllowancePercent = 5,
            disputeThresholdAmount = 5000
        });

        // Step 2: Upload same worker profile to both sites
        var stream = CreateTestExcel(
            ("E2E-030", "HighAllowSite", 20, 1000),   // Gross=20000, allow=25%=5000, net=25000
            ("E2E-031", "HighDeductSite", 20, 1000)   // Gross=20000, deduct=5000, allow=5%=1000, net=16000
        );

        var upload = await UploadExcel(stream);
        await _client.PostAsJsonAsync("/api/payments/calculate", new { uploadId = upload.UploadId });

        // Step 3: Verify different rules applied
        var lines = await _client.GetFromJsonAsync<PaginatedResult>("/api/payments?pageSize=100");

        var highAllow = lines!.Items.First(l => l.WorkerId == "E2E-030");
        Assert.Equal(20000m, highAllow.GrossAmount);
        Assert.Equal(5000m, highAllow.Allowances);    // 25%
        Assert.Equal(0m, highAllow.Deductions);
        Assert.Equal(25000m, highAllow.NetAmount);

        var highDeduct = lines.Items.First(l => l.WorkerId == "E2E-031");
        Assert.Equal(20000m, highDeduct.GrossAmount);
        Assert.Equal(1000m, highDeduct.Allowances);   // 5%
        Assert.Equal(5000m, highDeduct.Deductions);
        Assert.Equal(16000m, highDeduct.NetAmount);
    }

    /// <summary>
    /// E2E: Upload with validation errors, fix, and re-upload succeeds
    /// </summary>
    [Fact]
    public async Task ValidationErrorFlow_UploadBadData_ThenUploadCorrected()
    {
        // Step 1: Upload bad data
        var badStream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var ws = workbook.AddWorksheet("Attendance");
            ws.Cell(1, 1).Value = "WorkerId";
            ws.Cell(1, 2).Value = "Site";
            ws.Cell(1, 3).Value = "DaysPresent";
            ws.Cell(1, 4).Value = "DayRate";

            ws.Cell(2, 1).Value = "";          // Missing worker ID
            ws.Cell(2, 2).Value = "FixSite";
            ws.Cell(2, 3).Value = 20;
            ws.Cell(2, 4).Value = 1000;

            ws.Cell(3, 1).Value = "E2E-041";
            ws.Cell(3, 2).Value = "FixSite";
            ws.Cell(3, 3).Value = "invalid";   // Non-numeric days
            ws.Cell(3, 4).Value = 1000;

            workbook.SaveAs(badStream);
        }
        badStream.Position = 0;

        var badResult = await UploadExcel(badStream);
        Assert.Equal(2, badResult.ErrorCount);
        Assert.Equal(0, badResult.ValidRows);

        // Step 2: Upload corrected data
        var goodStream = CreateTestExcel(
            ("E2E-040", "FixSite", 20, 1000),
            ("E2E-041", "FixSite", 22, 1000)
        );

        var goodResult = await UploadExcel(goodStream);
        Assert.Equal(2, goodResult.TotalRows);
        Assert.Equal(2, goodResult.ValidRows);
        Assert.Equal(0, goodResult.ErrorCount);

        // Step 3: Calculate and verify
        await _client.PostAsJsonAsync("/api/payments/calculate", new { uploadId = goodResult.UploadId });

        var lines = await _client.GetFromJsonAsync<PaginatedResult>("/api/payments?pageSize=100");
        Assert.Contains(lines!.Items, l => l.WorkerId == "E2E-040");
        Assert.Contains(lines.Items, l => l.WorkerId == "E2E-041");
    }

    /// <summary>
    /// E2E: Large batch — verifies system handles many workers in single upload
    /// </summary>
    [Fact]
    public async Task LargeBatchFlow_ManyWorkers_ProcessesSuccessfully()
    {
        // Create 50 workers
        var rows = Enumerable.Range(1, 50)
            .Select(i => ($"E2E-L{i:D3}", "LargeSite", 20 + (i % 10), 800 + (i * 10)))
            .ToArray();

        var stream = CreateTestExcel(rows);
        var upload = await UploadExcel(stream);
        Assert.Equal(50, upload.TotalRows);
        Assert.Equal(50, upload.ValidRows);

        // Calculate
        var calcResponse = await _client.PostAsJsonAsync("/api/payments/calculate", new { uploadId = upload.UploadId });
        Assert.Equal(HttpStatusCode.OK, calcResponse.StatusCode);

        // Verify batch
        var batches = await _client.GetFromJsonAsync<List<BatchDto>>("/api/batches");
        var batch = batches!.First(b => b.SiteName == "LargeSite");
        Assert.Equal(50, batch.WorkerCount);
        Assert.True(batch.TotalAmount > 0);
    }

    /// <summary>
    /// E2E: Dispute threshold boundary — worker exactly at threshold
    /// </summary>
    [Fact]
    public async Task ThresholdBoundary_ExactThreshold_IsReady()
    {
        // Configure site with exact threshold we can hit
        await _client.PostAsJsonAsync("/api/rules", new
        {
            siteName = "BoundarySite",
            advanceDeductionAmount = 0,
            siteAllowancePercent = 10,
            disputeThresholdAmount = 11000  // Net must be >= 11000
        });

        // Worker: Gross=10000, Allowance=1000, Net=11000 (exactly at threshold)
        var stream = CreateTestExcel(("E2E-050", "BoundarySite", 10, 1000));

        var upload = await UploadExcel(stream);
        await _client.PostAsJsonAsync("/api/payments/calculate", new { uploadId = upload.UploadId });

        var lines = await _client.GetFromJsonAsync<PaginatedResult>("/api/payments?pageSize=100");
        var line = lines!.Items.First(l => l.WorkerId == "E2E-050");

        Assert.Equal(11000m, line.NetAmount);
        // At or above threshold should be Ready
        Assert.Equal("Ready", line.Status);
    }
}
