namespace IDADRS.Application.DTOs;
public sealed record UserResponseDto(
    int      Id,
    string   Username,
    string   Email,
    string   Role,
    DateTime CreatedDate);
