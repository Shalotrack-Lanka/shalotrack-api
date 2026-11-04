using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.Models;

/// <summary>
/// One "change my sign-in phone number" challenge: a code texted to the NEW number, bound to ONE customer.
/// Separate from <see cref="PhoneVerification"/> (the login OTP) on purpose: a login code for a number must never
/// be usable to change somebody's account, and a change code must never log anybody in.
///
/// The code is never stored in plaintext (CodeHash = SHA-256 of "{code}:{Salt}"). A row is single use:
/// ConsumedAt is claimed atomically when the right code is entered. AppliedAt is set only when the number
/// really changed; the "one change per 24 hours" rule reads it.
/// </summary>
public class PhoneChangeVerification
{
    [Key]
    public Guid PhoneChangeVerificationId { get; set; }

    public Guid CustomerId { get; set; }

    /// <summary>E.164 of the number the code was sent to, e.g. "+94771234567".</summary>
    public string NewPhoneNumber { get; set; } = string.Empty;

    public string CodeHash { get; set; } = string.Empty;

    public string Salt { get; set; } = string.Empty;

    /// <summary>Wrong guesses against this row. Locked at 5.</summary>
    public int AttemptCount { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime ExpiresAt { get; set; }

    /// <summary>The code was used (or the challenge was cancelled). Never verifiable again.</summary>
    public DateTime? ConsumedAt { get; set; }

    /// <summary>The number really was changed at this moment.</summary>
    public DateTime? AppliedAt { get; set; }

    /// <summary>Abuse forensics only, never an auth decision.</summary>
    public string? RequestIp { get; set; }
}
