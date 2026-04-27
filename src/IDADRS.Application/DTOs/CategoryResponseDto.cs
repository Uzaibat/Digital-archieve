namespace IDADRS.Application.DTOs;
public sealed record CategoryResponseDto(int Id, string CategoryName, string? Description, int DocumentCount);
