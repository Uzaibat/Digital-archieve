using IDADRS.Application.DTOs;
namespace IDADRS.Application.Interfaces;

public interface IAuthService
{
    Task<ApiResponse<LoginResponseDto>> LoginAsync(LoginRequestDto dto, CancellationToken ct = default);
    Task<ApiResponse<UserResponseDto>>  RegisterAsync(RegisterRequestDto dto, CancellationToken ct = default);
    Task<ApiResponse<LoginResponseDto>> RefreshAsync(RefreshTokenRequestDto dto, CancellationToken ct = default);
}
