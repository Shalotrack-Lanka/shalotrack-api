namespace ShaloTrack_API.Services.Interfaces;

public interface IPushNotificationService
{
    /// <summary>
    /// Sends a push notification to every device registered for this customer.
    /// A customer can have multiple tokens (multiple devices/reinstalls) --
    /// sends to all of them. Failures on individual tokens (expired, invalid)
    /// are logged and skipped, not thrown -- one bad token shouldn't block
    /// delivery to the customer's other devices.
    /// </summary>
    Task SendAlertPushAsync(Guid customerId, string title, string body);
}