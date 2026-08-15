namespace ShaloTrack_API.DTOs.SavedPlace;

public class CreateSavedPlaceDto
{
    public string Name { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
}