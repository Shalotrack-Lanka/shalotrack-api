using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

/// <summary>
/// Sends SMS via text.lk's v3 API (https://text.lk/docs/send-sms/). Uses the named
/// "TextLk" HttpClient registered in Program.cs -- BaseAddress and the Authorization:
/// Bearer header are set once there, from config injected via SSM, same pattern as
/// GatewayCommandClient.
/// </summary>
public class TextLkSmsSender : ISmsSender
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TextLkSmsSender> _logger;
    private readonly string _senderId;

    public TextLkSmsSender(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TextLkSmsSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _senderId = configuration["TextLk:SenderId"]
            ?? throw new InvalidOperationException(
                "TextLk:SenderId is not configured. Must be injected via the " +
                "TextLk__SenderId environment variable (an alphanumeric sender id " +
                "registered with text.lk, or a phone number with country code).");
    }

    public async Task SendAsync(string e164PhoneNumber, string message, CancellationToken cancellationToken = default)
    {
        // text.lk wants the number without the leading '+' (e.g. "94771234567").
        var recipient = e164PhoneNumber.TrimStart('+');

        var client = _httpClientFactory.CreateClient("TextLk");

        using var response = await client.PostAsJsonAsync(
            "/api/v3/sms/send",
            new TextLkSendRequest
            {
                Recipient = recipient,
                SenderId = _senderId,
                Type = "plain",
                Message = message
            },
            cancellationToken);

        var body = await response.Content.ReadFromJsonAsync<TextLkSendResponse>(
            cancellationToken: cancellationToken);

        if (!response.IsSuccessStatusCode || body is null || !string.Equals(body.Status, "success", StringComparison.OrdinalIgnoreCase))
        {
            // Deliberately not logging the message body (it contains the OTP code) --
            // only the vendor's own status/message and the response code.
            _logger.LogError(
                "text.lk send failed. HTTP {StatusCode}, vendor status={VendorStatus}, vendor message={VendorMessage}",
                (int)response.StatusCode, body?.Status, body?.Message);

            throw new InvalidOperationException(
                $"text.lk SMS send failed: {body?.Message ?? response.StatusCode.ToString()}");
        }
    }

    private class TextLkSendRequest
    {
        [JsonPropertyName("recipient")]
        public string Recipient { get; set; } = string.Empty;

        [JsonPropertyName("sender_id")]
        public string SenderId { get; set; } = string.Empty;

        [JsonPropertyName("type")]
        public string Type { get; set; } = "plain";

        [JsonPropertyName("message")]
        public string Message { get; set; } = string.Empty;
    }

    private class TextLkSendResponse
    {
        [JsonPropertyName("status")]
        public string? Status { get; set; }

        [JsonPropertyName("message")]
        public string? Message { get; set; }
    }
}
