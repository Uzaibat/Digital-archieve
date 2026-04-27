namespace IDADRS.Application.DTOs;
public sealed record DocumentResponseDto(
    int      Id,
    string   Title,
    string?  Description,
    string   FilePath,
    DateTime UploadDate,
    int?     UploadedBy,
    string?  UploaderUsername,
    int      CategoryId,
    string   CategoryName,
    string?  DownloadUrl,
    double   Score = 0d);
