namespace ShaloTrack_API.DTOs.Account;

/// <summary>
/// Everything ShaloTrack holds about the signed-in customer, in one readable document ("download my
/// data"). Deliberately excludes: push tokens and other credentials, bank-slip images, and other
/// people's personal details (the other side of a share appears only as a masked phone number).
/// Raw GPS history is not embedded (it can be millions of points): the Trip History page and its
/// PDF/CSV export give it per vehicle and date range.
/// </summary>
public class AccountExportDto
{
    public DateTime GeneratedAtUtc { get; set; }
    public string Note { get; set; } = string.Empty;
    public ExportProfileDto Profile { get; set; } = new();
    public List<ExportVehicleDto> Vehicles { get; set; } = new();
    public List<ExportReminderDto> Reminders { get; set; } = new();
    public List<ExportPlaceDto> SavedPlaces { get; set; } = new();
    public List<ExportGeofenceDto> Geofences { get; set; } = new();
    public List<ExportEmergencyContactDto> EmergencyContacts { get; set; } = new();
    public List<ExportComplaintDto> Complaints { get; set; } = new();
    public List<ExportRenewalDto> Renewals { get; set; } = new();
    public List<ExportShareDto> VehicleSharesGiven { get; set; } = new();
    public List<ExportShareDto> VehicleSharesReceived { get; set; } = new();
    public List<ExportAlertDto> RecentAlerts { get; set; } = new();
    public ExportDevicesDto Notifications { get; set; } = new();
    public int ActiveLiveLinks { get; set; }
}

public class ExportProfileDto
{
    public Guid CustomerId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string NicNumber { get; set; } = string.Empty;
    public string? Address { get; set; }
    public string AccountStatus { get; set; } = string.Empty;
    public bool WeeklySummaryEnabled { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ExportVehicleDto
{
    public Guid VehicleId { get; set; }
    public string VehicleNumber { get; set; } = string.Empty;
    public string? ChassisNumber { get; set; }
    public string? EngineNumber { get; set; }
    public string Make { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int Year { get; set; }
    public string? Color { get; set; }
    public string? VehicleType { get; set; }
    public string? FuelType { get; set; }
    public int SpeedLimitKmh { get; set; }
    public int? IdleAlertMinutes { get; set; }
    public string? DeviceImei { get; set; }
    public DateTime? DeviceBoundAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ExportReminderDto
{
    public string VehicleNumber { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string DueDate { get; set; } = string.Empty;
    public string? Notes { get; set; }
}

public class ExportPlaceDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int RadiusMeters { get; set; }
    public int VisitCount { get; set; }
    public DateTime? LastVisitedAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ExportGeofenceDto
{
    public string Name { get; set; } = string.Empty;
    public string? VehicleNumber { get; set; }
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int RadiusMeters { get; set; }
    public bool AlertOnEnter { get; set; }
    public bool AlertOnExit { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ExportEmergencyContactDto
{
    public string Name { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public string? Relationship { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ExportComplaintDto
{
    public string VehicleNumber { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public List<ExportComplaintReplyDto> Replies { get; set; } = new();
}

public class ExportComplaintReplyDto
{
    public string From { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ExportRenewalDto
{
    public string VehicleNumber { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? AmountLkr { get; set; }
    public string? PaymentReference { get; set; }
    public string? CustomerNote { get; set; }
    public bool SlipUploaded { get; set; }
    public string? DecisionReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
}

public class ExportShareDto
{
    public string VehicleNumber { get; set; } = string.Empty;

    /// <summary>The other person, masked (only the last 3 digits of the phone are shown).</summary>
    public string OtherPartyPhoneMasked { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime InvitedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}

public class ExportAlertDto
{
    public string VehicleNumber { get; set; } = string.Empty;
    public string AlertType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public DateTime TriggeredAt { get; set; }
}

/// <summary>Which devices receive push notifications. The tokens themselves are credentials and are not exported.</summary>
public class ExportDevicesDto
{
    public List<ExportPushDeviceDto> PushDevices { get; set; } = new();
}

public class ExportPushDeviceDto
{
    public string Platform { get; set; } = string.Empty;
    public DateTime LastSeenAt { get; set; }
}