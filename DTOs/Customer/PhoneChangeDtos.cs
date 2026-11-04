using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.DTOs.Customer;

public class RequestPhoneChangeDto
{
    [Required]
    [MaxLength(25)]
    public string PhoneNumber { get; set; } = string.Empty;
}

public class VerifyPhoneChangeDto
{
    [Required]
    [MaxLength(25)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [RegularExpression("^[0-9]{6}$", ErrorMessage = "The code is 6 digits.")]
    public string Code { get; set; } = string.Empty;
}

public class PhoneChangeRequestedDto
{
    /// <summary>How long the client should wait before offering "resend".</summary>
    public int ResendAfterSeconds { get; set; }
}

public class PhoneChangeResultDto
{
    /// <summary>The number now on the account (E.164).</summary>
    public string PhoneNumber { get; set; } = string.Empty;
}
