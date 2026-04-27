namespace IDADRS.Application.DTOs;
public sealed record UpdateUserDto(
    string Username,
    string Email,
    string Role);
