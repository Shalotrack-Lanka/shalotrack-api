namespace ShaloTrack_API.DTOs.VehicleReminder;

public class VehicleReminderResponseDto
{
    public Guid ReminderId { get; set; }
    public Guid VehicleId { get; set; }
    public int Type { get; set; }
    public DateOnly DueDate { get; set; }

    /// <summary>Whole days from today (Sri Lanka) to the due date; negative when overdue.</summary>
    public int DaysLeft { get; set; }

    public string? Notes { get; set; }
}