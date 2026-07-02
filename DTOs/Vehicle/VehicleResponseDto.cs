namespace ShaloTrack_API.DTOs.Vehicle;

public class VehicleResponseDto
{
    public Guid VehicleId { get; set; }

    public Guid CustomerId { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string VehicleNumber { get; set; } = string.Empty;

    public string? ChassisNumber { get; set; }

    public string? EngineNumber { get; set; }

    public string Make { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public int Year { get; set; }

    public string? Color { get; set; }

    public string? VehicleType { get; set; }

    public string? FuelType { get; set; }

    public bool HasGpsDevice { get; set; }
}