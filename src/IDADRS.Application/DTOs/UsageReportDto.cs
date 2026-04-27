namespace IDADRS.Application.DTOs;

public sealed record UsageReportDto(
    int                            TotalDocuments,
    int                            TotalCategories,
    int                            TotalUsers,
    long                           TotalAccessEvents,
    IReadOnlyList<DocumentSummary> RecentUploads);

public sealed record DocumentSummary(int Id, string Title, DateTime UploadDate, string CategoryName);
