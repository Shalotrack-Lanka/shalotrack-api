using ShaloTrack_API.Enums;
using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.Models;

public class Customer
{
    [Key]
    public Guid CustomerId { get; set; }

    // NEW — links this record to the Firebase account that owns it.
    // Nullable so existing rows migrate cleanly; backfill, then make required.
    public string? FirebaseUid { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string NicNumber { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string? ProfileImage { get; set; }
    public CustomerStatus AccountStatus { get; set; }

    // NEW -- weekly summary push. On by default (existing rows too: the column is added with
    // DEFAULT true, see ShaloTrackDbContext). LastWeeklySummaryFor is the Monday of the last week
    // a summary was handled for; it is claimed atomically before sending, so a restart or a
    // second instance can never send the same week twice. No separate log table is needed.
    public bool WeeklySummaryEnabled { get; set; } = true;
    public DateOnly? LastWeeklySummaryFor { get; set; }

    // Account deletion (PDPA). DeletionRequestedAt set = the account is locked and will be erased 30
    // days later unless cancelled (set back to null). DeletedAt set = erased: the row stays only as an
    // anonymised shell so payment history and complaint records keep their references.
    public DateTime? DeletionRequestedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ICollection<Vehicle> Vehicles { get; set; } = new List<Vehicle>();
}