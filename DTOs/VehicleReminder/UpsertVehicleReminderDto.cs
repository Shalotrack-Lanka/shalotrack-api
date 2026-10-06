namespace ShaloTrack_API.DTOs.VehicleReminder;

public class UpsertVehicleReminderDto
{
    /// <summary>0 = RevenueLicence, 1 = Insurance, 2 = ServiceDue.</summary>
    public int Type { get; set; }

    /// <summary>Calendar date, "yyyy-MM-dd".</summary>
    public DateOnly DueDate { get; set; }

    public string? Notes { get; set; }
}