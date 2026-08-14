using FirebaseAdmin.Messaging;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class PushNotificationService : IPushNotificationService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PushNotificationService> _logger;

    public PushNotificationService(IUnitOfWork unitOfWork, ILogger<PushNotificationService> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task SendAlertPushAsync(Guid customerId, string title, string body)
    {
        var tokens = await _unitOfWork.FcmTokens.GetByCustomerAsync(customerId);

        if (tokens.Count == 0)
        {
            _logger.LogInformation("No FCM tokens registered for customer {CustomerId}, skipping push.", customerId);
            return;
        }

        foreach (var token in tokens)
        {
            try
            {
#pragma warning disable CS0618 // Token is marked obsolete in SDK, but required for FCM registration tokens
                var message = new Message
                {
                    Token = token.FcmToken,
                    Notification = new Notification
                    {
                        Title = title,
                        Body = body
                    }
                };
#pragma warning restore CS0618

                await FirebaseMessaging.DefaultInstance.SendAsync(message);
                _logger.LogInformation("Push sent to customer {CustomerId}, token ending ...{TokenTail}",
                    customerId, token.FcmToken.Length > 6 ? token.FcmToken[^6..] : token.FcmToken);
            }
            catch (FirebaseMessagingException ex)
            {
                // A single bad token (expired, uninstalled app, etc.) should not
                // block delivery to this customer's other registered devices.
                _logger.LogWarning(ex, "Failed to send push to a token for customer {CustomerId}: {ErrorCode}",
                    customerId, ex.MessagingErrorCode);
            }
        }
    }
}