using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.Models;

/// <summary>
/// A temporary, revocable, no-login link to one vehicle's live position. Only the SHA-256 hash of
/// the token is stored: a database read (or a log line, or a backup) can never be turned back into
/// a working link. The raw token exists once, in the response that creates the link.
///
/// A link is usable while RevokedAt is null and ExpiresAt is in the future. The public page shows
/// the trail only from CreatedAt onwards, so sharing never exposes where the vehicle was earlier.
/// </summary>
public class LiveShareLink
{
    [Key]
    public Guid LinkId { get; set; }

    public Guid VehicleId { get; set; }

    /// <summary>The owner who created it (always the vehicle's owner).</summary>
    public Guid CustomerId { get; set; }

    /// <summary>Lower-case hex SHA-256 of the raw token (64 chars). Unique.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }
}