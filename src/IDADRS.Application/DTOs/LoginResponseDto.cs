namespace IDADRS.Application.DTOs;
public sealed record LoginResponseDto(
    string AccessToken,
    string RefreshToken,
    string Username,
    string Role,
    DateTime ExpiresAt);
