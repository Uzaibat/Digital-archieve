using IDADRS.Application.DTOs;
using IDADRS.Application.Interfaces;
using IDADRS.Core.Entities;
using IDADRS.Core.Interfaces;

namespace IDADRS.Application.Services;

public sealed class CategoryService : ICategoryService
{
    private readonly IUnitOfWork _uow;
    public CategoryService(IUnitOfWork uow) => _uow = uow;

    public async Task<ApiResponse<IReadOnlyList<CategoryResponseDto>>> GetAllAsync(CancellationToken ct = default)
    {
        var cats = await _uow.Categories.GetAllAsync(ct);
        return ApiResponse<IReadOnlyList<CategoryResponseDto>>.Ok(
            cats.Select(c => new CategoryResponseDto(c.Id, c.CategoryName, c.Description,
                                                     c.Documents.Count)).ToList());
    }

    public async Task<ApiResponse<CategoryResponseDto>> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var c = await _uow.Categories.GetByIdAsync(id, ct);
        if (c is null) return ApiResponse<CategoryResponseDto>.Fail("Category not found.");
        return ApiResponse<CategoryResponseDto>.Ok(new(c.Id, c.CategoryName, c.Description, c.Documents.Count));
    }

    public async Task<ApiResponse<CategoryResponseDto>> CreateAsync(CategoryCreateDto dto, CancellationToken ct = default)
    {
        var cat = new Category { CategoryName = dto.CategoryName.Trim(), Description = dto.Description?.Trim() };
        await _uow.Categories.AddAsync(cat, ct);
        await _uow.SaveAsync(ct);
        return ApiResponse<CategoryResponseDto>.Ok(new(cat.Id, cat.CategoryName, cat.Description, 0), "Created.");
    }

    public async Task<ApiResponse<CategoryResponseDto>> UpdateAsync(int id, CategoryCreateDto dto, CancellationToken ct = default)
    {
        var c = await _uow.Categories.GetByIdAsync(id, ct);
        if (c is null) return ApiResponse<CategoryResponseDto>.Fail("Category not found.");
        c.CategoryName = dto.CategoryName.Trim();
        c.Description  = dto.Description?.Trim();
        await _uow.Categories.UpdateAsync(c, ct);
        await _uow.SaveAsync(ct);
        return ApiResponse<CategoryResponseDto>.Ok(new(c.Id, c.CategoryName, c.Description, c.Documents.Count));
    }

    public async Task<ApiResponse<object?>> DeleteAsync(int id, CancellationToken ct = default)
    {
        var c = await _uow.Categories.GetByIdAsync(id, ct);
        if (c is null) return ApiResponse<object?>.Fail("Category not found.");
        if (c.Documents.Count > 0) return ApiResponse<object?>.Fail("Cannot delete a category that contains documents.");
        await _uow.Categories.DeleteAsync(c, ct);
        await _uow.SaveAsync(ct);
        return ApiResponse<object?>.Ok("Category deleted.");
    }
}
