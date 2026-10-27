using ShaloTrack_API.DTOs.Auth;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

public interface IPhoneVerificationService
{
    /// <summary>Generates and SMSes a one-time code for the given phone number via text.lk.</summary>
    Task<ApiResponse<object>> RequestOtpAsync(string rawPhoneNumber, string? requestIp, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the code and, on success, returns a Firebase custom token for the phone's
    /// account (creating the Firebase account first if this number has never signed in before).
    /// </summary>
    Task<ApiResponse<VerifyOtpResultDto>> VerifyOtpAsync(string rawPhoneNumber, string code, CancellationToken cancellationToken = default);
}
