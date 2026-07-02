using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.DTOs.Vehicle;

public class CreateVehicleDto
{
    [Required]
    public Guid CustomerId { get; set; }

    [Required]
    [MaxLength(20)]
    public string VehicleNumber { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? ChassisNumber { get; set; }

    [MaxLength(100)]
    public string? EngineNumber { get; set; }

    [Required]
    [MaxLength(50)]
    public string Make { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Model { get; set; } = string.Empty;

    [Range(1900, 2100)]
    public int Year { get; set; }

    [MaxLength(30)]
    public string? Color { get; set; }

    [MaxLength(30)]
    public string? VehicleType { get; set; }

    [MaxLength(30)]
    public string? FuelType { get; set; }
}