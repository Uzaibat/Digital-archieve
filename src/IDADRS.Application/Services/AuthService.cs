using IDADRS.Application.DTOs;
using IDADRS.Application.Interfaces;
using IDADRS.Core.Entities;
using IDADRS.Core.Enums;
using IDADRS.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace IDADRS.Application.Services;

/// <summary>
/// Handles registration, login, and JWT refresh token rotation.
/// BCrypt cost factor: 12 (matches §5 requirement).
///
/// SOLID note — SRP: this class only manages auth flow.
/// JWT generation is delegated to IJwtService (DIP).
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly IUnitOfWork _uow;
    private readonly IJwtService _jwt;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IUnitOfWork uow, IJwtService jwt, ILogger<AuthService> logger)
    {
        _uow    = uow;
        _jwt    = jwt;
        _logger = logger;
    }

    // ── Register ──────────────────────────────────────────────────────────────
    public async Task<ApiResponse<UserResponseDto>> RegisterAsync(
        RegisterRequestDto dto, CancellationToken ct = default)
    {
        if (await _uow.Users.ExistsByUsernameAsync(dto.Username, ct))
            return ApiResponse<UserResponseDto>.Fail("Username already taken.");

        if (await _uow.Users.ExistsByEmailAsync(dto.Email, ct))
            return ApiResponse<UserResponseDto>.Fail("Email already registered.");

        var user = new User
        {
            Username     = dto.Username.Trim(),
            Email        = dto.Email.Trim().ToLowerInvariant(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password, workFactor: 12),
            Role         = UserRole.User,
            CreatedDate  = DateTime.UtcNow,
        };

        await _uow.Users.AddAsync(user, ct);
        await _uow.SaveAsync(ct);

        _logger.LogInformation("New user registered: {Username}", user.Username);

        return ApiResponse<UserResponseDto>.Ok(MapUser(user), "Registration successful.");
    }

    // ── Login ─────────────────────────────────────────────────────────────────
    public async Task<ApiResponse<LoginResponseDto>> LoginAsync(
        LoginRequestDto dto, CancellationToken ct = default)
    {
        var user = await _uow.Users.GetByUsernameAsync(dto.Username, ct);

        if (user is null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for username: {Username}", dto.Username);
            return ApiResponse<LoginResponseDto>.Fail("Invalid username or password.");
        }

        return ApiResponse<LoginResponseDto>.Ok(
            await IssueTokensAsync(user, ct),
            "Login successful.");
    }

    // ── Refresh ───────────────────────────────────────────────────────────────
    public async Task<ApiResponse<LoginResponseDto>> RefreshAsync(
        RefreshTokenRequestDto dto, CancellationToken ct = default)
    {
        var existing = await _uow.RefreshTokens.GetActiveAsync(dto.RefreshToken, ct);

        if (existing is null)
            return ApiResponse<LoginResponseDto>.Fail("Invalid or expired refresh token.");

        // Revoke old token — rotation strategy
        existing.Revoked = true;

        var user    = await _uow.Users.GetByIdAsync(existing.UserId, ct);
        if (user is null)
            return ApiResponse<LoginResponseDto>.Fail("User account not found.");

        var response             = await IssueTokensAsync(user, ct);
        existing.ReplacedBy      = response.RefreshToken;

        await _uow.SaveAsync(ct);

        _logger.LogInformation("Refresh token rotated for user {UserId}", user.Id);

        return ApiResponse<LoginResponseDto>.Ok(response, "Token refreshed.");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────
    private async Task<LoginResponseDto> IssueTokensAsync(User user, CancellationToken ct)
    {
        var accessToken  = _jwt.GenerateAccessToken(user);
        var refreshToken = _jwt.GenerateRefreshToken();
        var expiresAt    = _jwt.AccessTokenExpiry();

        var token = new RefreshToken
        {
            UserId    = user.Id,
            Token     = refreshToken,
            IssuedAt  = DateTime.UtcNow,
            ExpiresAt = _jwt.RefreshTokenExpiry(),
            Revoked   = false,
        };
        await _uow.RefreshTokens.AddAsync(token, ct);

        return new LoginResponseDto(accessToken, refreshToken, user.Username,
                                    user.Role.ToString(), expiresAt);
    }

    private static UserResponseDto MapUser(User u) =>
        new(u.Id, u.Username, u.Email, u.Role.ToString(), u.CreatedDate);
}
