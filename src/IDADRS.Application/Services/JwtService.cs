using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using IDADRS.Application.Interfaces;
using IDADRS.Core.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace IDADRS.Application.Services;

/// <summary>
/// Generates and validates JWT access tokens + opaque refresh tokens.
/// Access token lifetime : 15 minutes.
/// Refresh token lifetime: 7 days.
/// </summary>
public sealed class JwtService : IJwtService
{
    private readonly string   _secret;
    private readonly string   _issuer;
    private readonly string   _audience;
    private readonly TimeSpan _accessLifetime  = TimeSpan.FromMinutes(15);
    private readonly TimeSpan _refreshLifetime = TimeSpan.FromDays(7);

    public JwtService(IConfiguration config)
    {
        _secret   = config["Jwt:Secret"]   ?? throw new InvalidOperationException("Jwt:Secret not configured.");
        _issuer   = config["Jwt:Issuer"]   ?? "IDADRS";
        _audience = config["Jwt:Audience"] ?? "IDADRS-Client";
    }

    /// <inheritdoc />
    public string GenerateAccessToken(User user)
    {
        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name,           user.Username),
            new Claim(ClaimTypes.Email,          user.Email),
            new Claim(ClaimTypes.Role,           user.Role.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            issuer:             _issuer,
            audience:           _audience,
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            AccessTokenExpiry(),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <inheritdoc />
    public string GenerateRefreshToken()
    {
        // 64 cryptographically random bytes → base-64 URL-safe string
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes)
               .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    /// <inheritdoc />
    public DateTime AccessTokenExpiry()  => DateTime.UtcNow.Add(_accessLifetime);

    /// <inheritdoc />
    public DateTime RefreshTokenExpiry() => DateTime.UtcNow.Add(_refreshLifetime);

    /// <inheritdoc />
    public string? GetUsernameFromToken(string accessToken)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var jwt     = handler.ReadJwtToken(accessToken);
            return jwt.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;
        }
        catch
        {
            return null;
        }
    }
}
