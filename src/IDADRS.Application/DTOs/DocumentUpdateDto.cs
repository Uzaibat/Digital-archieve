namespace IDADRS.Application.DTOs;
public sealed record DocumentUpdateDto(
    string  Title,
    string? Description,
    int     CategoryId);
