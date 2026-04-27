using System.Security.Claims;
using IDADRS.Application.DTOs;
using IDADRS.Application.Interfaces;
using IDADRS.Core.Entities;
using IDADRS.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace IDADRS.API.Controllers;

/// <summary>Document CRUD, multipart upload, signed download URL, and full-text search.</summary>
[ApiController]
[Route("api/documents")]
[Authorize(Policy = "AnyRole")]
public sealed class DocumentsController : ControllerBase
{
    private readonly IDocumentService    _docs;
    private readonly IFileStorageService _storage;
    private readonly IUnitOfWork         _uow;

    public DocumentsController(IDocumentService docs, IFileStorageService storage, IUnitOfWork uow)
    { _docs = docs; _storage = storage; _uow = uow; }

    /// <summary>List all documents.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<DocumentResponseDto>>), 200)]
    public async Task<IActionResult> GetAll(CancellationToken ct) => Ok(await _docs.GetAllAsync(ct));

    /// <summary>Get a single document with a 1-hour signed download URL.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiResponse<DocumentResponseDto>), 200)]
    [ProducesResponseType(typeof(ApiResponse<object?>), 404)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var r = await _docs.GetByIdAsync(id, ct);
        return r.Success ? Ok(r) : NotFound(r);
    }

    /// <summary>Upload a new document (multipart/form-data). Max 50 MB. Types: PDF DOCX XLSX PNG JPG.</summary>
    [HttpPost]
    [Authorize(Policy = "ArchivistPlus")]
    [RequestSizeLimit(52_428_800)]
    [ProducesResponseType(typeof(ApiResponse<DocumentResponseDto>), 201)]
    [ProducesResponseType(typeof(ApiResponse<object?>), 400)]
    public async Task<IActionResult> Create([FromForm] DocumentCreateDto dto, CancellationToken ct)
    {
        var uploaderId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var r = await _docs.CreateAsync(dto, uploaderId, ct);
        return r.Success ? CreatedAtAction(nameof(GetById), new { id = r.Data!.Id }, r) : BadRequest(r);
    }

    /// <summary>Update document metadata (title, description, category).</summary>
    [HttpPut("{id:int}")]
    [Authorize(Policy = "ArchivistPlus")]
    [ProducesResponseType(typeof(ApiResponse<DocumentResponseDto>), 200)]
    public async Task<IActionResult> Update(int id, [FromBody] DocumentUpdateDto dto, CancellationToken ct)
    {
        var r = await _docs.UpdateAsync(id, dto, ct);
        return r.Success ? Ok(r) : BadRequest(r);
    }

    /// <summary>Delete a document and its stored file.</summary>
    [HttpDelete("{id:int}")]
    [Authorize(Policy = "ArchivistPlus")]
    [ProducesResponseType(typeof(ApiResponse<object?>), 200)]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var r = await _docs.DeleteAsync(id, ct);
        return r.Success ? Ok(r) : NotFound(r);
    }

    /// <summary>Full-text search — PostgreSQL GIN index + native C TF-IDF re-rank.</summary>
    [HttpGet("search")]
    [ProducesResponseType(typeof(ApiResponse<IReadOnlyList<DocumentResponseDto>>), 200)]
    public async Task<IActionResult> Search(
        [FromQuery] string? query,
        [FromQuery] int? categoryId,
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        return Ok(await _docs.SearchAsync(query, categoryId, from, to, page, pageSize, ct));
    }

    [HttpGet("{id:int}/download")]
    [Authorize]
    public async Task<IActionResult> Download(int id, CancellationToken ct)
    {
        var doc = await _uow.Documents.GetByIdAsync(id, ct);
        if (doc == null) return NotFound(ApiResponse.Fail("Document not found"));

        var filePath = Path.IsPathRooted(doc.FilePath)
            ? doc.FilePath
            : Path.Combine(Directory.GetCurrentDirectory(), "uploads", doc.FilePath.Replace('/', Path.DirectorySeparatorChar));
        if (!System.IO.File.Exists(filePath))
            return NotFound(ApiResponse.Fail("File not found on disk"));

        await _uow.AccessLogs.AddAsync(new AccessLog
        {
            UserId = GetCurrentUserId(),
            DocumentId = id,
            AccessDate = DateTime.UtcNow,
            ActionType = "Download"
        }, ct);
        await _uow.SaveAsync(ct);

        var ext = Path.GetExtension(filePath).TrimStart('.').ToLowerInvariant();
        var mimeType = ext switch
        {
            "pdf" => "application/pdf",
            "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "png" => "image/png",
            "jpg" or "jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };

        var bytes = await System.IO.File.ReadAllBytesAsync(filePath, ct);
        return File(bytes, mimeType, Path.GetFileName(filePath));
    }

    /// <summary>Serve a file after validating the HMAC-SHA256 signed URL.</summary>
    [HttpGet("file/{*relativePath}")]
    [AllowAnonymous]
    public IActionResult ServeFile(string relativePath, [FromQuery] string sig, [FromQuery] long expires)
    {
        if (!_storage.ValidateSignedUrl(relativePath, sig, expires)) return Forbid();
        var abs = Path.Combine(Directory.GetCurrentDirectory(), "uploads",
                               relativePath.Replace('/', Path.DirectorySeparatorChar));
        if (!System.IO.File.Exists(abs)) return NotFound();
        var mime = relativePath.Split('.').Last().ToLowerInvariant() switch
        {
            "pdf"  => "application/pdf",
            "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "png"  => "image/png",
            _      => "image/jpeg",
        };
        return PhysicalFile(abs, mime, enableRangeProcessing: true);
    }

    private int GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var id) ? id : 0;
    }
}
