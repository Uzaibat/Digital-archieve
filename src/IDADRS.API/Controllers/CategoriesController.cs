using IDADRS.Application.DTOs;
using IDADRS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IDADRS.API.Controllers;

/// <summary>Category management — full CRUD.</summary>
[ApiController]
[Route("api/categories")]
[Authorize(Policy = "AnyRole")]
public sealed class CategoriesController : ControllerBase
{
    private readonly ICategoryService _cats;
    public CategoriesController(ICategoryService cats) => _cats = cats;

    /// <summary>List all categories with document counts.</summary>
    [HttpGet]  public async Task<IActionResult> GetAll(CancellationToken ct) => Ok(await _cats.GetAllAsync(ct));

    /// <summary>Get single category.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    { var r = await _cats.GetByIdAsync(id, ct); return r.Success ? Ok(r) : NotFound(r); }

    /// <summary>Create category (Archivist+).</summary>
    [HttpPost]
    [Authorize(Policy = "ArchivistPlus")]
    public async Task<IActionResult> Create([FromBody] CategoryCreateDto dto, CancellationToken ct)
    { var r = await _cats.CreateAsync(dto, ct); return r.Success ? CreatedAtAction(nameof(GetById), new { id = r.Data!.Id }, r) : BadRequest(r); }

    /// <summary>Update category (Archivist+).</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = "ArchivistPlus")]
    public async Task<IActionResult> Update(int id, [FromBody] CategoryCreateDto dto, CancellationToken ct)
    { var r = await _cats.UpdateAsync(id, dto, ct); return r.Success ? Ok(r) : BadRequest(r); }

    /// <summary>Delete empty category (Admin only).</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    { var r = await _cats.DeleteAsync(id, ct); return r.Success ? Ok(r) : BadRequest(r); }
}
