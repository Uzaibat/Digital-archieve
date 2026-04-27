using IDADRS.Application.DTOs;
namespace IDADRS.Application.Interfaces;

public interface IDocumentService
{
    Task<ApiResponse<IReadOnlyList<DocumentResponseDto>>> GetAllAsync(CancellationToken ct = default);
    Task<ApiResponse<DocumentResponseDto>>               GetByIdAsync(int id, CancellationToken ct = default);
    Task<ApiResponse<DocumentResponseDto>>               CreateAsync(DocumentCreateDto dto, int uploaderId, CancellationToken ct = default);
    Task<ApiResponse<DocumentResponseDto>>               UpdateAsync(int id, DocumentUpdateDto dto, CancellationToken ct = default);
    Task<ApiResponse<object?>>                           DeleteAsync(int id, CancellationToken ct = default);
    Task<ApiResponse<IReadOnlyList<DocumentResponseDto>>> SearchAsync(
        string? query,
        int? categoryId,
        DateTime? from,
        DateTime? to,
        int page = 1,
        int pageSize = 20,
        CancellationToken ct = default);
}
