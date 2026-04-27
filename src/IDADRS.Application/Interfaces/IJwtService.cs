using IDADRS.Core.Entities;
namespace IDADRS.Application.Interfaces;

public interface IJwtService
{
    /// <summary>Generates a short-lived access token (15 min).</summary>
    string GenerateAccessToken(User user);

    /// <summary>Generates a long-lived opaque refresh token (7 days).</summary>
    string GenerateRefreshToken();

    /// <summary>Returns the expiry DateTime for a new access token.</summary>
    DateTime AccessTokenExpiry();

    /// <summary>Returns the expiry DateTime for a new refresh token.</summary>
    DateTime RefreshTokenExpiry();

    /// <summary>Extracts the username from a token without validating signature.</summary>
    string? GetUsernameFromToken(string accessToken);
}
