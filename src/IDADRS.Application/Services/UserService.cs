using IDADRS.Application.DTOs;
using IDADRS.Application.Interfaces;
using IDADRS.Core.Enums;
using IDADRS.Core.Interfaces;

namespace IDADRS.Application.Services;

public sealed class UserService : IUserService
{
    private readonly IUnitOfWork _uow;
    public UserService(IUnitOfWork uow) => _uow = uow;

    public async Task<ApiResponse<IReadOnlyList<UserResponseDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var users = await _uow.Users.GetAllAsync(ct);
        return ApiResponse<IReadOnlyList<UserResponseDto>>.Ok(
            users.Select(u => new UserResponseDto(u.Id, u.Username, u.Email,
                                                  u.Role.ToString(), u.CreatedDate)).ToList());
    }

    public async Task<ApiResponse<UserResponseDto>> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var u = await _uow.Users.GetByIdAsync(id, ct);
        if (u is null) return ApiResponse<UserResponseDto>.Fail("User not found.");
        return ApiResponse<UserResponseDto>.Ok(new(u.Id, u.Username, u.Email, u.Role.ToString(), u.CreatedDate));
    }

    public async Task<ApiResponse<UserResponseDto>> UpdateAsync(int id, UpdateUserDto dto, CancellationToken ct = default)
    {
        var u = await _uow.Users.GetByIdAsync(id, ct);
        if (u is null) return ApiResponse<UserResponseDto>.Fail("User not found.");

        if (!Enum.TryParse<UserRole>(dto.Role, out var role))
            return ApiResponse<UserResponseDto>.Fail($"Invalid role '{dto.Role}'.");

        u.Username = dto.Username.Trim();
        u.Email    = dto.Email.Trim().ToLowerInvariant();
        u.Role     = role;
        await _uow.Users.UpdateAsync(u, ct);
        await _uow.SaveAsync(ct);
        return ApiResponse<UserResponseDto>.Ok(new(u.Id, u.Username, u.Email, u.Role.ToString(), u.CreatedDate));
    }

    public async Task<ApiResponse<object?>> DeleteAsync(int id, CancellationToken ct = default)
    {
        var u = await _uow.Users.GetByIdAsync(id, ct);
        if (u is null) return ApiResponse<object?>.Fail("User not found.");
        await _uow.Users.DeleteAsync(u, ct);
        await _uow.SaveAsync(ct);
        return ApiResponse<object?>.Ok("User deleted.");
    }
}
