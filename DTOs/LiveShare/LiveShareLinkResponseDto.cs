namespace ShaloTrack_API.DTOs.LiveShare;

public class LiveShareLinkResponseDto
{
    public Guid LinkId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// The raw token. Present ONLY in the response to the create call; every later read returns
    /// null because the API keeps only a hash. If the owner loses it, they stop sharing and make a new link.
    /// </summary>
    public string? Token { get; set; }
}