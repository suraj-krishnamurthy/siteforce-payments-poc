using Microsoft.AspNetCore.Mvc;
using SiteForce.PaymentApi.DTOs;
using SiteForce.PaymentApi.Services;

namespace SiteForce.PaymentApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UploadController : ControllerBase
{
    private readonly IngestionService _ingestionService;
    private const long MaxFileSize = 10 * 1024 * 1024; // 10 MB

    public UploadController(IngestionService ingestionService)
    {
        _ingestionService = ingestionService;
    }

    /// <summary>
    /// Upload an Excel file containing attendance data.
    /// Expected columns: WorkerId, Site, DaysPresent, DayRate
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(MaxFileSize)]
    public async Task<ActionResult<UploadResultDto>> Upload(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file provided");

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (extension != ".xlsx")
            return BadRequest("Only .xlsx files are accepted");

        if (file.Length > MaxFileSize)
            return BadRequest("File size exceeds 10 MB limit");

        // POC: hardcoded demo user (production inherits SiteForce JWT)
        var uploadedBy = Request.Headers["X-User-Name"].FirstOrDefault() ?? "demo-user";

        using var stream = file.OpenReadStream();
        var result = await _ingestionService.IngestExcelAsync(stream, file.FileName, uploadedBy);

        return Ok(new UploadResultDto
        {
            UploadId = result.UploadId,
            TotalRows = result.TotalRows,
            ValidRows = result.ValidRows,
            ErrorCount = result.ErrorCount,
            Errors = result.Errors,
            Duplicates = result.Duplicates
        });
    }

    /// <summary>
    /// Confirm overwrite of duplicate attendance records.
    /// </summary>
    [HttpPost("confirm-overwrite")]
    public async Task<IActionResult> ConfirmOverwrite([FromBody] ConfirmOverwriteDto request)
    {
        var actorName = Request.Headers["X-User-Name"].FirstOrDefault() ?? "demo-user";

        try
        {
            await _ingestionService.ConfirmOverwriteAsync(request.UploadId, actorName);
            return Ok(new { message = "Duplicates overwritten successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Cancel an upload that has pending duplicates.
    /// </summary>
    [HttpPost("cancel")]
    public async Task<IActionResult> CancelUpload([FromBody] ConfirmOverwriteDto request)
    {
        var actorName = Request.Headers["X-User-Name"].FirstOrDefault() ?? "demo-user";

        try
        {
            await _ingestionService.CancelUploadAsync(request.UploadId, actorName);
            return Ok(new { message = "Upload cancelled successfully." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }
}
