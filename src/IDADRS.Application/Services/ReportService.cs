using System.Text;
using IDADRS.Application.DTOs;
using IDADRS.Application.Interfaces;
using IDADRS.Core.Interfaces;

namespace IDADRS.Application.Services;

/// <summary>
/// Produces usage stats, audit log, and CSV-based PDF/Excel exports.
/// PDF: lightweight HTML→text via StringBuilder (swap QuestPDF for production).
/// Excel: RFC-4180 CSV (swap ClosedXML for production column types).
/// </summary>
public sealed class ReportService : IReportService
{
    private readonly IUnitOfWork _uow;
    public ReportService(IUnitOfWork uow) => _uow = uow;

    public async Task<ApiResponse<UsageReportDto>> GetUsageReportAsync(CancellationToken ct = default)
    {
        var docCount      = await _uow.Documents.CountAsync(ct);
        var catCount      = await _uow.Categories.CountAsync(ct);
        var userCount     = await _uow.Users.CountAsync(ct);
        var accessCount   = await _uow.AccessLogs.CountAsync(ct);
        var recentUploads = await _uow.Documents.GetRecentAsync(10, ct);

        return ApiResponse<UsageReportDto>.Ok(new(
            docCount, catCount, userCount, accessCount,
            recentUploads.Select(d => new DocumentSummary(
                d.Id, d.Title, d.UploadDate, d.Category?.CategoryName ?? "–")).ToList()));
    }

    public async Task<ApiResponse<AuditReportDto>> GetAuditReportAsync(
        int page, int pageSize, CancellationToken ct = default)
    {
        var (entries, total) = await _uow.AccessLogs.GetPagedAsync(page, pageSize, ct);
        return ApiResponse<AuditReportDto>.Ok(new(
            entries.Select(e => new AuditEntryDto(
                e.Id, e.User?.Username ?? "?",
                e.Document?.Title ?? "?",
                e.ActionType, e.AccessDate)).ToList(), total));
    }

    public async Task<byte[]> ExportPdfAsync(CancellationToken ct = default)
    {
        var (report, _) = await BuildReportDataAsync(ct);
        var sb = new StringBuilder();
        sb.AppendLine("IDADRS — Usage Report");
        sb.AppendLine($"Generated: {DateTime.UtcNow:u}");
        sb.AppendLine();
        sb.AppendLine($"Total Documents : {report.TotalDocuments}");
        sb.AppendLine($"Total Categories: {report.TotalCategories}");
        sb.AppendLine($"Total Users     : {report.TotalUsers}");
        sb.AppendLine($"Total Access Evt: {report.TotalAccessEvents}");
        sb.AppendLine();
        sb.AppendLine("Recent Uploads:");
        foreach (var d in report.RecentUploads)
            sb.AppendLine($"  [{d.Id}] {d.Title} — {d.CategoryName} — {d.UploadDate:yyyy-MM-dd}");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public async Task<byte[]> ExportExcelAsync(CancellationToken ct = default)
    {
        var (report, audit) = await BuildReportDataAsync(ct);
        var sb = new StringBuilder();
        sb.AppendLine("Id,Title,CategoryName,UploadDate");
        foreach (var d in report.RecentUploads)
            sb.AppendLine($"{d.Id},\"{EscapeCsv(d.Title)}\",\"{EscapeCsv(d.CategoryName)}\",{d.UploadDate:yyyy-MM-dd}");
        sb.AppendLine();
        sb.AppendLine("AuditId,Username,DocumentTitle,ActionType,AccessDate");
        foreach (var e in audit)
            sb.AppendLine($"{e.Id},\"{EscapeCsv(e.Username)}\",\"{EscapeCsv(e.DocumentTitle)}\",{e.ActionType},{e.AccessDate:yyyy-MM-ddTHH:mm:ssZ}");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private async Task<(UsageReportDto report, IEnumerable<AuditEntryDto> audit)> BuildReportDataAsync(CancellationToken ct)
    {
        var usageResult = await GetUsageReportAsync(ct);
        var auditResult = await GetAuditReportAsync(1, 1000, ct);
        return (usageResult.Data!, auditResult.Data!.Entries);
    }

    private static string EscapeCsv(string? s) => (s ?? string.Empty).Replace("\"", "\"\"");
}
