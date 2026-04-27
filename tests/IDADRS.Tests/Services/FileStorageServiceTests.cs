using IDADRS.Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace IDADRS.Tests.Services;

public sealed class FileStorageServiceTests : IDisposable
{
    private readonly FileStorageService _sut;
    private readonly string _tmp = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    public FileStorageServiceTests()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:RootPath"]   = _tmp,
            ["Storage:HmacSecret"] = "test-hmac-secret-minimum-length-ok",
            ["Storage:BaseUrl"]    = "https://test.example.com",
        }).Build();
        _sut = new FileStorageService(cfg, NullLogger<FileStorageService>.Instance);
    }

    public void Dispose() { if (Directory.Exists(_tmp)) Directory.Delete(_tmp, true); }

    [Fact] public void GenerateSignedUrl_ContainsSigAndExpires()
    { var url = _sut.GenerateSignedUrl("2026/04/abc.pdf"); Assert.Contains("sig=", url); Assert.Contains("expires=", url); }

    [Fact] public void ValidateSignedUrl_Valid_ReturnsTrue()
    {
        const string path = "2026/04/abc.pdf";
        var url     = new Uri(_sut.GenerateSignedUrl(path));
        var qs      = System.Web.HttpUtility.ParseQueryString(url.Query);
        Assert.True(_sut.ValidateSignedUrl(path, qs["sig"]!, long.Parse(qs["expires"]!)));
    }

    [Fact] public void ValidateSignedUrl_WrongSig_ReturnsFalse()
    { Assert.False(_sut.ValidateSignedUrl("2026/04/abc.pdf", "wrongsig", DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds())); }

    [Fact] public void ValidateSignedUrl_Expired_ReturnsFalse()
    {
        var url = new Uri(_sut.GenerateSignedUrl("2026/04/abc.pdf"));
        var qs  = System.Web.HttpUtility.ParseQueryString(url.Query);
        Assert.False(_sut.ValidateSignedUrl("2026/04/abc.pdf", qs["sig"]!,
            DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeSeconds()));
    }
}
