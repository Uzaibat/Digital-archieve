using IDADRS.Application.DTOs;
namespace IDADRS.Application.Interfaces;

public interface ICategoryService
{
    Task<ApiResponse<IReadOnlyList<CategoryResponseDto>>> GetAllAsync(CancellationToken ct = default);
    Task<ApiResponse<CategoryResponseDto>>               GetByIdAsync(int id, CancellationToken ct = default);
    Task<ApiResponse<CategoryResponseDto>>               CreateAsync(CategoryCreateDto dto, CancellationToken ct = default);
    Task<ApiResponse<CategoryResponseDto>>               UpdateAsync(int id, CategoryCreateDto dto, CancellationToken ct = default);
    Task<ApiResponse<object?>>                           DeleteAsync(int id, CancellationToken ct = default);
}
