using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.Models;

/// <summary>
/// The bank slip image/PDF, kept in its own table so list queries never drag the bytes along.
/// One slip per request (a re-upload replaces it). Capped at 2 MB and type-checked by content, not
/// by the client's claimed type.
/// </summary>
public class RenewalSlip
{
    [Key]
    public Guid RenewalRequestId { get; set; }

    public string ContentType { get; set; } = string.Empty;
    public byte[] Content { get; set; } = Array.Empty<byte>();
    public int SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }

    public RenewalRequest Request { get; set; } = null!;
}