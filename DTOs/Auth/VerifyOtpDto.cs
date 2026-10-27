using System.ComponentModel.DataAnnotations;

namespace ShaloTrack_API.DTOs.Auth;

public class VerifyOtpDto
{
    [Required]
    [StringLength(20, MinimumLength = 9)]
    public string PhoneNumber { get; set; } = string.Empty;

    [Required]
    [StringLength(6, MinimumLength = 6)]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "Code must be exactly 6 digits.")]
    public string Code { get; set; } = string.Empty;
}

public class VerifyOtpResultDto
{
    /// <summary>
    /// A Firebase custom token. The client calls FirebaseAuth signInWithCustomToken(this),
    /// which the Firebase SDK exchanges for a normal Firebase ID token -- every other
    /// endpoint keeps working completely unchanged against that ID token.
    /// </summary>
    public string CustomToken { get; set; } = string.Empty;

    /// <summary>True if no Customer profile exists yet for this phone's Firebase account -- tells the app to show the "create your profile" screen instead of going straight to the dashboard.</summary>
    public bool IsNewCustomer { get; set; }
}
