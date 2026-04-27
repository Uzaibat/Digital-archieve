using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using IDADRS.Application.Services;
using IDADRS.Core.Entities;
using IDADRS.Core.Enums;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace IDADRS.Tests.Services;

public sealed class JwtServiceTests
{
    private readonly JwtService _sut;
    private readonly User _user = new()
    { Id = 42, Username = "testuser", Email = "test@idadrs.local", Role = UserRole.Archivist };

    public JwtServiceTests()
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Secret"]   = "test-secret-key-minimum-32-characters!!",
            ["Jwt:Issuer"]   = "IDADRS-Test",
            ["Jwt:Audience"] = "IDADRS-Test-Client",
        }).Build();
        _sut = new JwtService(cfg);
    }

    [Fact] public void GenerateAccessToken_ReturnsNonEmpty() => Assert.NotEmpty(_sut.GenerateAccessToken(_user));

    [Fact] public void GenerateAccessToken_ContainsUsername()
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(_sut.GenerateAccessToken(_user));
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.Name && c.Value == "testuser");
    }

    [Fact] public void GenerateAccessToken_ContainsRole()
    {
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(_sut.GenerateAccessToken(_user));
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.Role && c.Value == "Archivist");
    }

    [Fact] public void AccessTokenExpiry_Is15Minutes()
    {
        var exp = _sut.AccessTokenExpiry();
        Assert.True(exp > DateTime.UtcNow.AddMinutes(14) && exp < DateTime.UtcNow.AddMinutes(16));
    }

    [Fact] public void RefreshTokenExpiry_Is7Days()
    {
        var exp = _sut.RefreshTokenExpiry();
        Assert.True(exp > DateTime.UtcNow.AddDays(6) && exp < DateTime.UtcNow.AddDays(8));
    }

    [Fact] public void GenerateRefreshToken_UniqueEachCall()
        => Assert.NotEqual(_sut.GenerateRefreshToken(), _sut.GenerateRefreshToken());

    [Fact] public void GetUsernameFromToken_ReturnsUsername()
        => Assert.Equal("testuser", _sut.GetUsernameFromToken(_sut.GenerateAccessToken(_user)));

    [Fact] public void GetUsernameFromToken_InvalidToken_ReturnsNull()
        => Assert.Null(_sut.GetUsernameFromToken("bad.token"));
}
