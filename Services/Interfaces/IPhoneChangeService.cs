using ShaloTrack_API.DTOs.Customer;
using ShaloTrack_API.Responses;

namespace ShaloTrack_API.Services.Interfaces;

/// <summary>
/// Changing the sign-in phone number of the signed-in customer: a code to the NEW number, then verify.
/// The phone number is the account's only identity proof, so this is an account-takeover target and is
/// deliberately slow and strict. See PhoneChangeService for the rules.
/// </summary>
public interface IPhoneChangeService
{
    /// <summary>Step 1: text a one-time code to the new number.</summary>
    Task<ApiResponse<PhoneChangeRequestedDto>> RequestAsync(RequestPhoneChangeDto dto, string? requestIp, CancellationToken cancellationToken = default);

    /// <summary>Step 2: check the code; on success move the number on the customer record AND the Firebase login.</summary>
    Task<ApiResponse<PhoneChangeResultDto>> VerifyAsync(VerifyPhoneChangeDto dto, CancellationToken cancellationToken = default);
}
