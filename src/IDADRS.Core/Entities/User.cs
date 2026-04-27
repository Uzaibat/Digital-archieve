using System.ComponentModel.DataAnnotations;
using IDADRS.Core.Enums;

namespace IDADRS.Core.Entities;

/// <summary>Represents a system user with role-based access.</summary>
public class User
{
    public int    Id           { get; set; }

    /// <summary>Unique display name, max 60 characters.</summary>
    [MaxLength(60)]
    public string Username     { get; set; } = string.Empty;

    /// <summary>Validated e-mail address, unique in the system.</summary>
    [MaxLength(200)]
    [EmailAddress]
    public string Email        { get; set; } = string.Empty;

    /// <summary>BCrypt hash (cost factor 12).</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public UserRole  Role        { get; set; } = UserRole.User;
    public DateTime  CreatedDate { get; set; } = DateTime.UtcNow;

    public ICollection<Document>     Documents  { get; set; } = new List<Document>();
    public ICollection<AccessLog>    AccessLogs { get; set; } = new List<AccessLog>();
    public ICollection<RefreshToken> Tokens     { get; set; } = new List<RefreshToken>();
}
