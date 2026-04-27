namespace IDADRS.Application.DTOs;
public sealed record RegisterRequestDto(
    string Username,
    string Email,
    string Password);
