namespace ShaloTrack_API.DTOs.SavedPlace;

public class SavedPlaceResponseDto
{
    public Guid PlaceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public int RadiusMeters { get; set; }
    public int VisitCount { get; set; }
    public DateTime? LastVisitedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    // No Address field here deliberately -- resolving it is the Android
    // client's job via the existing free AddressResolver (Android's
    // built-in Geocoder), same pattern already used everywhere else in
    // this app (Trip History, live tracking). Adding server-side
    // reverse-geocoding here would mean a new paid API dependency for
    // something already solved for free.
}