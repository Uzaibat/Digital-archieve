using IDADRS.Application.DTOs;
using IDADRS.Application.Interfaces;
using IDADRS.Core.Entities;
using IDADRS.Core.Interfaces;
using IDADRS.NativeSearch;
using Microsoft.Extensions.Logging;

namespace IDADRS.Application.Services;

public sealed class DocumentService : IDocumentService
{
    private readonly IUnitOfWork           _uow;
    private readonly IFileStorageService   _storage;
    private readonly INativeSearchService  _native;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(IUnitOfWork uow, IFileStorageService storage,
                           INativeSearchService native, ILogger<DocumentService> logger)
    {
        _uow     = uow;
        _storage = storage;
        _native  = native;
        _logger  = logger;
    }

    public async Task<ApiResponse<IReadOnlyList<DocumentResponseDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var docs = await _uow.Documents.GetAllDetailedAsync(ct);
        return ApiResponse<IReadOnlyList<DocumentResponseDto>>.Ok(
            docs.Select(d => Map(d, null)).ToList());
    }

    public async Task<ApiResponse<DocumentResponseDto>> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var doc = await _uow.Documents.GetDetailedByIdAsync(id, ct);
        if (doc is null) return ApiResponse<DocumentResponseDto>.Fail("Document not found.");
        var url = _storage.GenerateSignedUrl(doc.FilePath);
        return ApiResponse<DocumentResponseDto>.Ok(Map(doc, url));
    }

    public async Task<ApiResponse<DocumentResponseDto>> CreateAsync(
        DocumentCreateDto dto, int uploaderId, CancellationToken ct = default)
    {
        var category = await _uow.Categories.GetByIdAsync(dto.CategoryId, ct);
        if (category is null) return ApiResponse<DocumentResponseDto>.Fail("Category not found.");

        string filePath;
        try { filePath = await UploadAsync(dto.File, ct); }
        catch (InvalidOperationException ex)
        { return ApiResponse<DocumentResponseDto>.Fail(ex.Message); }

        var doc = new Document
        {
            Title       = dto.Title.Trim(),
            Description = dto.Description?.Trim(),
            FilePath    = filePath,
            UploadDate  = DateTime.UtcNow,
            UploadedBy  = uploaderId,
            CategoryId  = dto.CategoryId,
        };

        await _uow.Documents.AddAsync(doc, ct);
        await _uow.SaveAsync(ct);

        _logger.LogInformation("Document {Id} '{Title}' created by user {UploaderId}",
                               doc.Id, doc.Title, uploaderId);

        var created = await _uow.Documents.GetDetailedByIdAsync(doc.Id, ct);
        return ApiResponse<DocumentResponseDto>.Ok(Map(created!, null), "Document uploaded.");
    }

    public async Task<ApiResponse<DocumentResponseDto>> UpdateAsync(
        int id, DocumentUpdateDto dto, CancellationToken ct = default)
    {
        var doc = await _uow.Documents.GetByIdAsync(id, ct);
        if (doc is null) return ApiResponse<DocumentResponseDto>.Fail("Document not found.");

        doc.Title       = dto.Title.Trim();
        doc.Description = dto.Description?.Trim();
        doc.CategoryId  = dto.CategoryId;

        await _uow.Documents.UpdateAsync(doc, ct);
        await _uow.SaveAsync(ct);

        var updated = await _uow.Documents.GetDetailedByIdAsync(id, ct);
        return ApiResponse<DocumentResponseDto>.Ok(Map(updated!, null), "Document updated.");
    }

    public async Task<ApiResponse<object?>> DeleteAsync(int id, CancellationToken ct = default)
    {
        var doc = await _uow.Documents.GetByIdAsync(id, ct);
        if (doc is null) return ApiResponse<object?>.Fail("Document not found.");

        await _storage.DeleteAsync(doc.FilePath, ct);
        await _uow.Documents.DeleteAsync(doc, ct);
        await _uow.SaveAsync(ct);

        _logger.LogInformation("Document {Id} deleted", id);
        return ApiResponse<object?>.Ok("Document deleted.");
    }

    public async Task<ApiResponse<IReadOnlyList<DocumentResponseDto>>> SearchAsync(
        string? query, int? categoryId, DateTime? from, DateTime? to, int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var hasQuery = !string.IsNullOrWhiteSpace(query);
        var hasFilters = categoryId.HasValue || from.HasValue || to.HasValue;

        IEnumerable<Document> baseResults;
        if (!hasQuery && !hasFilters)
        {
            baseResults = await _uow.Documents.GetRecentAsync(20, ct);
        }
        else if (hasQuery)
        {
            try
            {
                baseResults = await _uow.Documents.FullTextSearchAsync(query!, ct);
            }
            catch
            {
                var all = await _uow.Documents.GetAllDetailedAsync(ct);
                baseResults = all.Where(d =>
                    d.Title.Contains(query!, StringComparison.OrdinalIgnoreCase) ||
                    (d.Description?.Contains(query!, StringComparison.OrdinalIgnoreCase) ?? false));
            }
        }
        else
        {
            baseResults = await _uow.Documents.GetAllDetailedAsync(ct);
        }

        if (categoryId.HasValue) baseResults = baseResults.Where(d => d.CategoryId == categoryId.Value);
        if (from.HasValue) baseResults = baseResults.Where(d => d.UploadDate >= from.Value);
        if (to.HasValue) baseResults = baseResults.Where(d => d.UploadDate <= to.Value);

        var scored = baseResults
            .Select(d =>
            {
                var score = hasQuery
                    ? _native.ScoreDocument(query!, $"{d.Title} {d.Description ?? string.Empty}")
                    : 1d;
                score = Math.Max(0d, Math.Min(1d, score));
                return new { Doc = d, Score = score };
            })
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Doc.UploadDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => Map(x.Doc, null, x.Score))
            .ToList();

        return ApiResponse<IReadOnlyList<DocumentResponseDto>>.Ok(scored);
    }

    private static DocumentResponseDto Map(Document d, string? url, double score = 0d) =>
        new(d.Id, d.Title, d.Description, d.FilePath, d.UploadDate,
            d.UploadedBy, d.Uploader?.Username, d.CategoryId,
            d.Category?.CategoryName ?? string.Empty, url, score);

    // Keeps uploads portable by storing relative paths from the storage root.
    private Task<string> UploadAsync(Microsoft.AspNetCore.Http.IFormFile file, CancellationToken ct = default)
        => _storage.SaveAsync(file, ct);
}
