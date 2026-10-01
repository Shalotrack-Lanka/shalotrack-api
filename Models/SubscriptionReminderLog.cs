using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.Models;

/// <summary>
/// One row per renewal reminder actually sent, so nobody is reminded twice for the same
/// expiry. The unique index on (ImeiNumber, Milestone, ExpiresAt) is the dedupe guard: the
/// row is inserted BEFORE the push, so a second worker (or a restart mid-run) cannot send it
/// again. A renewal produces a new ExpiresAt, so the next cycle gets fresh reminders.
/// </summary>
public class SubscriptionReminderLog
{
    [Key]
    public Guid ReminderId { get; set; }

    public string ImeiNumber { get; set; } = string.Empty;

    /// <summary>Days before expiry this reminder was for (14, 7, 1). 0 = the "subscription ended" notice.</summary>
    public int Milestone { get; set; }

    /// <summary>The expiry date this reminder belongs to (copied from DeviceSubscriptionStatus.ExpiresAt).</summary>
    public DateTime ExpiresAt { get; set; }

    public DateTime SentAt { get; set; }
}