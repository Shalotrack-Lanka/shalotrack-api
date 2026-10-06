using System.ComponentModel.DataAnnotations;
using ShaloTrack_API.Enums;

namespace ShaloTrack_API.Models;

/// <summary>
/// A due date the owner wants to be reminded about (revenue licence, insurance, service).
/// At most one row per (vehicle, type): saving again replaces the date, which also starts a fresh
/// notification cycle because the notice ledger is keyed by DueDate.
/// </summary>
public class VehicleReminder
{
    [Key]
    public Guid ReminderId { get; set; }

    public Guid VehicleId { get; set; }

    public VehicleReminderType Type { get; set; }

    /// <summary>A calendar date (Sri Lanka local), deliberately not a timestamp.</summary>
    public DateOnly DueDate { get; set; }

    /// <summary>Optional short note, e.g. a policy or garage name. Max 200 chars. Never put in push text.</summary>
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public Vehicle Vehicle { get; set; } = null!;
}