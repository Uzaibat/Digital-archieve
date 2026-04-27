using Microsoft.AspNetCore.Http;
namespace IDADRS.Application.Interfaces;

public interface IFileStorageService
{
    /// <summary>Saves the file and returns the relative storage path.</summary>
    Task<string> SaveAsync(IFormFile file, CancellationToken ct = default);

    /// <summary>Deletes a file by its relative storage path.</summary>
    Task DeleteAsync(string relativePath, CancellationToken ct = default);

    /// <summary>Returns a signed HMAC-SHA256 URL valid for 1 hour.</summary>
    string GenerateSignedUrl(string relativePath);

    /// <summary>Validates that a signed URL is still valid and untampered.</summary>
    bool ValidateSignedUrl(string relativePath, string signature, long expiresUnix);
}
