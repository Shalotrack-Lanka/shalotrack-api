namespace ShaloTrack_API.Services.Interfaces;

/// <summary>
/// Abstraction over the SMS vendor so the OTP flow (PhoneVerificationService) never
/// talks to text.lk's HTTP API directly. Swapping vendors later -- or adding a second
/// one for failover -- touches only the implementation, not the auth flow.
/// </summary>
public interface ISmsSender
{
    /// <summary>
    /// Sends <paramref name="message"/> to <paramref name="e164PhoneNumber"/>.
    /// Throws on any failure (network, vendor error, bad credentials) -- the caller
    /// decides what a failed send means for the OTP request; this method never
    /// swallows an error into a false "sent".
    /// </summary>
    Task SendAsync(string e164PhoneNumber, string message, CancellationToken cancellationToken = default);
}
