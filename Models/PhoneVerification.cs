using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.Models;

/// <summary>
/// One OTP challenge for one phone number. Replaces Firebase Phone Auth's SMS delivery
/// (which Dialog/Airtel/Hutch were silently filtering -- see security-audit notes,
/// 2026-10-27) with our own code, sent via text.lk, while Firebase stays the session/
/// token issuer for everything downstream (custom token -> ID token, unchanged
/// [Authorize] chain, FCM, account deletion).
///
/// The code itself is never stored in plaintext -- only CodeHash (SHA-256 of
/// "{code}:{Salt}") -- so a DB read alone never discloses a usable code.
/// </summary>
public class PhoneVerification
{
    [Key]
    public Guid PhoneVerificationId { get; set; }

    /// <summary>E.164, e.g. "+94771234567". Normalized by the service before storing.</summary>
    public string PhoneNumber { get; set; } = string.Empty;

    public string CodeHash { get; set; } = string.Empty;

    public string Salt { get; set; } = string.Empty;

    /// <summary>Failed verify attempts against this row. Locked out at 5 -- see PhoneVerificationService.</summary>
    public int AttemptCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    /// <summary>Set the moment a verify succeeds. A consumed row can never be verified again -- blocks replay of a code sniffed or guessed after the fact.</summary>
    public DateTime? ConsumedAt { get; set; }

    /// <summary>Caller IP at request time. Abuse forensics only -- never used for auth decisions.</summary>
    public string? RequestIp { get; set; }
}
