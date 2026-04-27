using IDADRS.Application.DTOs;
namespace IDADRS.Application.Interfaces;

public interface IReportService
{
    Task<ApiResponse<UsageReportDto>> GetUsageReportAsync(CancellationToken ct = default);
    Task<ApiResponse<AuditReportDto>> GetAuditReportAsync(int page, int pageSize, CancellationToken ct = default);
    Task<byte[]>                      ExportPdfAsync(CancellationToken ct = default);
    Task<byte[]>                      ExportExcelAsync(CancellationToken ct = default);
}
