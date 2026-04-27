using System.ComponentModel.DataAnnotations;

namespace IDADRS.Core.Entities;

/// <summary>Audit record for every document interaction.</summary>
public class AccessLog
{
    public int      Id          { get; set; }
    public int      UserId      { get; set; }
    public int      DocumentId  { get; set; }
    public DateTime AccessDate  { get; set; } = DateTime.UtcNow;

    /// <summary>One of: Upload | View | Update | Delete | Download.</summary>
    [MaxLength(20)]
    public string ActionType { get; set; } = string.Empty;

    public User?     User     { get; set; }
    public Document? Document { get; set; }
}
