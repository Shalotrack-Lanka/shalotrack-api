namespace ShaloTrack_API.DTOs.Vehicle;

public class SnapToRoadPointDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class SnapToRoadRequestDto
{
    public List<SnapToRoadPointDto> Points { get; set; } = new();
}

public class SnappedPointDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    // Maps to which input point this snapped point corresponds to -- comes
    // straight through from Google's own response field of the same name.
    // Google may return fewer snapped points than requested (points far from
    // any known road can be dropped), so this is how the caller matches
    // snapped points back to the original input sequence.
    public int? OriginalIndex { get; set; }
}