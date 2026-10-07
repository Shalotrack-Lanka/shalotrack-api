namespace ShaloTrack_API.DTOs.LiveShare;

public class CreateLiveShareDto
{
    /// <summary>How long the link works, in whole hours (1 to 24).</summary>
    public int DurationHours { get; set; }
}