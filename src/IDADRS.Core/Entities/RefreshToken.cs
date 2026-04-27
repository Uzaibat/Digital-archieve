namespace IDADRS.Core.Entities;

/// <summary>Opaque refresh token — rotated on every use.</summary>
public class RefreshToken
{
    public int      Id          { get; set; }
    public int      UserId      { get; set; }
    public string   Token       { get; set; } = string.Empty;
    public DateTime IssuedAt    { get; set; }
    public DateTime ExpiresAt   { get; set; }
    public bool     Revoked     { get; set; }
    public string?  ReplacedBy  { get; set; }

    public User? User { get; set; }
}
