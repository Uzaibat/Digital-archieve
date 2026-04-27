using System.ComponentModel.DataAnnotations;

namespace IDADRS.Core.Entities;

/// <summary>Document classification category.</summary>
public class Category
{
    public int    Id           { get; set; }

    [MaxLength(100)]
    public string CategoryName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public ICollection<Document> Documents { get; set; } = new List<Document>();
}
