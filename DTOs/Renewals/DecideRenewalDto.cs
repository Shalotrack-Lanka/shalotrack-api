namespace ShaloTrack_API.DTOs.Renewals;

public class DecideRenewalDto
{
    /// <summary>approve | reject</summary>
    public string Decision { get; set; } = string.Empty;

    /// <summary>Required for reject (max 300 chars). Ignored for approve.</summary>
    public string? Reason { get; set; }

    /// <summary>Required for approve: the slip hash staff were shown. A changed slip is refused.</summary>
    public string? SlipSha256 { get; set; }

    /// <summary>Admin portal user id, for the record.</summary>
    public string? DecidedBy { get; set; }
}