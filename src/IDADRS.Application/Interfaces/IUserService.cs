using IDADRS.Application.DTOs;
namespace IDADRS.Application.Interfaces;

public interface IUserService
{
    Task<ApiResponse<IReadOnlyList<UserResponseDto>>> GetAllAsync(CancellationToken ct = default);
    Task<ApiResponse<UserResponseDto>>               GetByIdAsync(int id, CancellationToken ct = default);
    Task<ApiResponse<UserResponseDto>>               UpdateAsync(int id, UpdateUserDto dto, CancellationToken ct = default);
    Task<ApiResponse<object?>>                       DeleteAsync(int id, CancellationToken ct = default);
}
