using Veil.Domain.Users;

namespace Veil.Domain.Auth;

/// <summary>
/// A long-lived credential used to obtain new short-lived access tokens.
/// Only a SHA-256 hash of the token is stored, so a database leak does not
/// leak usable refresh tokens.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public AppUser User { get; set; } = null!;

    /// <summary>Base64 SHA-256 hash of the raw token value.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>
    /// All tokens produced by rotating the same login share a family id.
    /// If a revoked token from a family is reused, the whole family is revoked
    /// (a strong signal that the token was stolen).
    /// </summary>
    public Guid FamilyId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime ExpiresAtUtc { get; set; }

    public DateTime? RevokedAtUtc { get; set; }

    public Guid? ReplacedByTokenId { get; set; }

    public bool IsActive(DateTime nowUtc) => RevokedAtUtc is null && ExpiresAtUtc > nowUtc;
}
