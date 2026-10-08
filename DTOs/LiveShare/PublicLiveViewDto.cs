namespace ShaloTrack_API.DTOs.LiveShare;

/// <summary>
/// Everything an anonymous link holder can see. Deliberately small: plate number, current
/// position and the trail since the link was created. No customer details, no vehicle make/model,
/// no device identifiers, no history from before the link existed.
/// </summary>
public class PublicLiveViewDto
{
    public string PlateNumber { get; set; } = string.Empty;

    /// <summary>Car, SUV, Van, Truck, Motorcycle, Three-Wheeler ... Only used to pick the map icon.
    /// Not sensitive (visible on the car itself), unlike make/model which stay private.</summary>
    public string? VehicleType { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime ServerTime { get; set; }
    public PublicLivePositionDto Position { get; set; } = new();

    /// <summary>Null when the caller asked for the position only (cheaper poll).</summary>
    public List<PublicLiveTrailPointDto>? Trail { get; set; }
}

public class PublicLivePositionDto
{
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal SpeedKmh { get; set; }
    public decimal Heading { get; set; }
    public bool IsMoving { get; set; }
    public DateTime LastUpdate { get; set; }
}

public class PublicLiveTrailPointDto
{
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public DateTime Time { get; set; }
}