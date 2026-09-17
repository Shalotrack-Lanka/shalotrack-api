namespace ShaloTrack_API.DTOs.Complaint;

// Deserialized from Admin's dealer-lookup endpoint response at complaint
// filing time. DealerId null means no matching dealer lead was found --
// a valid, expected outcome (customer has no dealer), not an error.
public class DealerLookupResultDto
{
    public int? DealerId { get; set; }
    public string? DealerName { get; set; }
}