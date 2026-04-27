using System.Security.Cryptography;
using System.Text;
using IDADRS.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IDADRS.Application.Services;

/// <summary>
/// §8 File Storage — saves uploads to /uploads/{year}/{month}/{guid}.{ext},
/// validates MIME type + size, and generates HMAC-SHA256 signed download URLs.
/// </summary>
public sealed class FileStorageService : IFileStorageService
{
    private static readonly HashSet<string> AllowedMime = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",  // .docx
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",        // .xlsx
        "image/png",
        "image/jpeg",
    };

    private static readonly Dictionary<string, string> MimeToExt =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["application/pdf"]                  = ".pdf",
            ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"] = ".docx",
            ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"]       = ".xlsx",
            ["image/png"]  = ".png",
            ["image/jpeg"] = ".jpg",
        };

    private const long MaxBytes = 50L * 1024 * 1024;   // 50 MB

    private readonly string _rootPath;
    private readonly string _hmacSecret;
    private readonly string _baseUrl;
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(IConfiguration config, ILogger<FileStorageService> logger)
    {
        _rootPath   = config["Storage:RootPath"]  ?? Path.Combine(Directory.GetCurrentDirectory(), "uploads");
        _hmacSecret = config["Storage:HmacSecret"] ?? throw new InvalidOperationException("Storage:HmacSecret not configured.");
        _baseUrl    = config["Storage:BaseUrl"]    ?? "https://localhost:5001";
        _logger     = logger;
        Directory.CreateDirectory(_rootPath);
    }

    // ── Save ─────────────────────────────────────────────────────────────────
    public async Task<string> SaveAsync(IFormFile file, CancellationToken ct = default)
    {
        if (!AllowedMime.Contains(file.ContentType))
            throw new InvalidOperationException(
                $"File type '{file.ContentType}' is not allowed. " +
                "Permitted types: PDF, DOCX, XLSX, PNG, JPG.");

        if (file.Length > MaxBytes)
            throw new InvalidOperationException(
                $"File size {file.Length / 1024 / 1024} MB exceeds the 50 MB limit.");

        var ext      = MimeToExt.TryGetValue(file.ContentType, out var e) ? e : ".bin";
        var now      = DateTime.UtcNow;
        var relDir   = Path.Combine(now.Year.ToString(), now.Month.ToString("D2"));
        var fileName = $"{Guid.NewGuid():N}{ext}";
        var relPath  = Path.Combine(relDir, fileName).Replace('\\', '/');
        var absDir   = Path.Combine(_rootPath, relDir);
        var absPath  = Path.Combine(_rootPath, relDir, fileName);

        Directory.CreateDirectory(absDir);

        await using var stream = new FileStream(absPath, FileMode.CreateNew, FileAccess.Write,
                                                FileShare.None, bufferSize: 81920, useAsync: true);
        await file.CopyToAsync(stream, ct);

        _logger.LogInformation("Saved file {RelPath} ({Bytes} bytes)", relPath, file.Length);
        return relPath;
    }

    // ── Delete ────────────────────────────────────────────────────────────────
    public Task DeleteAsync(string relativePath, CancellationToken ct = default)
    {
        var absPath = Path.Combine(_rootPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(absPath))
        {
            File.Delete(absPath);
            _logger.LogInformation("Deleted file {RelPath}", relativePath);
        }
        return Task.CompletedTask;
    }

    // ── Signed URL ────────────────────────────────────────────────────────────
    /// <summary>
    /// Returns a URL of the form:
    ///   {baseUrl}/api/documents/file/{relativePath}?sig={hmac}&expires={unix}
    /// The HMAC covers: relativePath + "|" + expiresUnix.
    /// </summary>
    public string GenerateSignedUrl(string relativePath)
    {
        var expiresUnix = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var sig         = ComputeHmac(relativePath, expiresUnix);
        var encoded     = Uri.EscapeDataString(relativePath);
        return $"{_baseUrl}/api/documents/file/{encoded}?sig={sig}&expires={expiresUnix}";
    }

    /// <inheritdoc />
    public bool ValidateSignedUrl(string relativePath, string signature, long expiresUnix)
    {
        if (DateTimeOffset.UtcNow.ToUnixTimeSeconds() > expiresUnix) return false;
        var expected = ComputeHmac(relativePath, expiresUnix);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(signature));
    }

    // ── HMAC helper ───────────────────────────────────────────────────────────
    private string ComputeHmac(string relativePath, long expiresUnix)
    {
        var payload = $"{relativePath}|{expiresUnix}";
        var key     = Encoding.UTF8.GetBytes(_hmacSecret);
        var data    = Encoding.UTF8.GetBytes(payload);
        var hash    = HMACSHA256.HashData(key, data);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
