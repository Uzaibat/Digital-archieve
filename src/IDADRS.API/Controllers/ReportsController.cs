using IDADRS.Application.Interfaces;
using IDADRS.Application.DTOs;
using IDADRS.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IDADRS.API.Controllers;

/// <summary>Usage statistics, audit trail, and file exports.</summary>
[ApiController]
[Route("api/reports")]
[Authorize(Policy = "ArchivistPlus")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportService _reports;
    private readonly IUnitOfWork _uow;
    public ReportsController(IReportService reports, IUnitOfWork uow)
    {
        _reports = reports;
        _uow = uow;
    }

    /// <summary>Aggregate usage stats (doc count, categories, users, access events).</summary>
    [HttpGet("usage")]
    public async Task<IActionResult> Usage(CancellationToken ct)
    {
        var docs = await _uow.Documents.GetAllDetailedAsync(ct);
        var categories = await _uow.Categories.CountAsync(ct);
        var now = DateTime.UtcNow.Date;
        var sevenDaysAgo = now.AddDays(-7);
        var thirtyDaysAgo = now.AddDays(-29);

        var docsList = docs.ToList();
        var recentUploads = docsList.Count(d => d.UploadDate.Date >= sevenDaysAgo);
        long totalStorageBytes = 0;
        foreach (var d in docsList)
        {
            var abs = Path.Combine(Directory.GetCurrentDirectory(), "uploads", d.FilePath.Replace('/', Path.DirectorySeparatorChar));
            if (System.IO.File.Exists(abs))
                totalStorageBytes += new FileInfo(abs).Length;
        }

        var uploadsPerDay = Enumerable.Range(0, 30)
            .Select(i =>
            {
                var day = thirtyDaysAgo.AddDays(i);
                return new
                {
                    date = day.ToString("yyyy-MM-dd"),
                    count = docsList.Count(d => d.UploadDate.Date == day)
                };
            })
            .ToArray();

        return Ok(ApiResponse<object>.Ok(new
        {
            totalDocs = docsList.Count,
            totalCategories = categories,
            recentUploads,
            totalStorageBytes,
            uploadsPerDay
        }));
    }

    /// <summary>Paginated audit log of all access events.</summary>
    [HttpGet("audit")]
    public async Task<IActionResult> Audit([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
        => Ok(await _reports.GetAuditReportAsync(page, pageSize, ct));

    /// <summary>Export report as PDF text (Admin only).</summary>
    [HttpGet("export/pdf")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ExportPdf(CancellationToken ct)
    { var b = await _reports.ExportPdfAsync(ct); return File(b, "application/pdf", $"report-{DateTime.UtcNow:yyyyMMdd}.pdf"); }

    /// <summary>Export report as CSV (Admin only).</summary>
    [HttpGet("export/excel")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ExportExcel(CancellationToken ct)
    { var b = await _reports.ExportExcelAsync(ct); return File(b, "text/csv", $"report-{DateTime.UtcNow:yyyyMMdd}.csv"); }
}
