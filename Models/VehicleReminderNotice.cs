using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.Models;

/// <summary>
/// One row per reminder push actually sent. The unique index on (ReminderId, Threshold, DueDate)
/// is the dedupe guard: the row is inserted BEFORE the push, so a second worker or a restart
/// mid-run cannot send the same notice twice. Changing the due date yields new notices.
/// </summary>
public class VehicleReminderNotice
{
    [Key]
    public Guid NoticeId { get; set; }

    public Guid ReminderId { get; set; }

    /// <summary>Days before the due date (30, 14, 7, 1), 0 = due today, -1 = recently overdue.</summary>
    public int Threshold { get; set; }

    public DateOnly DueDate { get; set; }

    public DateTime SentAt { get; set; }
}