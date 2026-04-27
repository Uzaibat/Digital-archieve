using IDADRS.Application.DTOs;
using IDADRS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IDADRS.API.Controllers;

/// <summary>Authentication — login, register, JWT refresh.</summary>
[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    /// <summary>Authenticate with username + password. Returns JWT access + refresh tokens.</summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object?>), 401)]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto dto, CancellationToken ct)
    {
        var result = await _auth.LoginAsync(dto, ct);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    /// <summary>Register a new user account (default role: User).</summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResponse<object?>), 400)]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto dto, CancellationToken ct)
    {
        var result = await _auth.RegisterAsync(dto, ct);
        return result.Success ? CreatedAtAction(null, null, result) : BadRequest(result);
    }

    /// <summary>Exchange a valid refresh token for a new access + refresh token pair.</summary>
    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiResponse<LoginResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object?>), 401)]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDto dto, CancellationToken ct)
    {
        var result = await _auth.RefreshAsync(dto, ct);
        return result.Success ? Ok(result) : Unauthorized(result);
    }
}
