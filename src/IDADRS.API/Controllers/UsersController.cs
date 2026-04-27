using IDADRS.Application.DTOs;
using IDADRS.Application.Interfaces;
using IDADRS.Core.Enums;
using IDADRS.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IDADRS.API.Controllers;

/// <summary>User management — Admin only.</summary>
[ApiController]
[Route("api/users")]
[Authorize(Policy = "AdminOnly")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserService _users;
    private readonly IUnitOfWork _uow;
    public UsersController(IUserService users, IUnitOfWork uow)
    {
        _users = users;
        _uow = uow;
    }

    /// <summary>List all registered users.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<UserResponseDto>>), 200)]
    public async Task<IActionResult> GetAll(CancellationToken ct) => Ok(await _users.GetAllAsync(ct));

    /// <summary>Get user by ID.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object?>), 404)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var r = await _users.GetByIdAsync(id, ct);
        return r.Success ? Ok(r) : NotFound(r);
    }

    /// <summary>Update username, email, or role.</summary>
    [HttpPut("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<UserResponseDto>), 200)]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateUserDto dto, CancellationToken ct)
    {
        var r = await _users.UpdateAsync(id, dto, ct);
        return r.Success ? Ok(r) : BadRequest(r);
    }

    [HttpPut("{id:int}/role")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateRole(int id, [FromBody] UpdateRoleDto dto, CancellationToken ct)
    {
        if (!Enum.TryParse<UserRole>(dto.Role, true, out var role))
            return BadRequest(ApiResponse.Fail("Invalid role"));
        var user = await _uow.Users.GetByIdAsync(id, ct);
        if (user == null) return NotFound(ApiResponse.Fail("User not found"));
        user.Role = role;
        await _uow.Users.UpdateAsync(user, ct);
        await _uow.SaveAsync(ct);
        return Ok(ApiResponse.Ok("Role updated"));
    }

    /// <summary>Permanently delete a user account.</summary>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<object?>), 200)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var r = await _users.DeleteAsync(id, ct);
        return r.Success ? Ok(r) : NotFound(r);
    }
}

public sealed record UpdateRoleDto(string Role);
