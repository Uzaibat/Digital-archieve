using Microsoft.AspNetCore.Http;
namespace IDADRS.Application.DTOs;

public sealed record DocumentCreateDto(
    string      Title,
    string?     Description,
    int         CategoryId,
    IFormFile   File);
