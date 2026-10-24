using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.DTOs.SavedPlace;

public class CreateSavedPlaceDto
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "-90", "90")]
    public decimal Latitude { get; set; }

    [Range(typeof(decimal), "-180", "180")]
    public decimal Longitude { get; set; }
}