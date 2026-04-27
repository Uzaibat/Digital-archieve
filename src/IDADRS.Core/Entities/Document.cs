using System.ComponentModel.DataAnnotations;
using NpgsqlTypes;

namespace IDADRS.Core.Entities;

/// <summary>Archived document with metadata and storage path.</summary>
public class Document
{
    public int    Id           { get; set; }

    [MaxLength(200)]
    public string  Title        { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Description  { get; set; }

    public string  FilePath     { get; set; } = string.Empty;
    public DateTime UploadDate  { get; set; } = DateTime.UtcNow;
    public int?    UploadedBy   { get; set; }
    public int     CategoryId   { get; set; }

    public User?     Uploader   { get; set; }
    public Category? Category   { get; set; }

    /// <summary>PostgreSQL GIN tsvector column — auto-generated from Title + Description.</summary>
    public NpgsqlTsVector SearchVector { get; set; } = null!;

    public ICollection<AccessLog> AccessLogs { get; set; } = new List<AccessLog>();
}
