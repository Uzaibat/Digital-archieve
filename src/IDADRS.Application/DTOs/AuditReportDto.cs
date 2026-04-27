namespace IDADRS.Application.DTOs;

public sealed record AuditReportDto(IReadOnlyList<AuditEntryDto> Entries, int TotalCount);

public sealed record AuditEntryDto(
    int      Id,
    string   Username,
    string   DocumentTitle,
    string   ActionType,
    DateTime AccessDate);
