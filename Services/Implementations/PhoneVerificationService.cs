using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;
using ShaloTrack_API.DTOs.Auth;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

/// <summary>
/// Owns the full OTP lifecycle: generate -> SMS via text.lk -> verify -> mint a Firebase
/// custom token. See PhoneVerification.cs and ISmsSender.cs for why this exists instead of
/// Firebase Phone Auth (Dialog/Airtel/Hutch were silently dropping Firebase's SMS).
///
/// Every failure path returns the SAME generic message ("Invalid or expired code.") to the
/// caller regardless of the real reason (not found / expired / too many attempts / hash
/// mismatch) -- that distinction is only in the logs. Giving a different message per case
/// would let an attacker binary-search their way to information about a number that isn't
/// theirs.
/// </summary>
public class PhoneVerificationService : IPhoneVerificationService
{
    private const int CodeExpiryMinutes = 5;
    private const int MaxVerifyAttempts = 5;
    private const int ResendCooldownSeconds = 45;
    private const int MaxRequestsPerHourPerNumber = 5;

    private readonly ShaloTrackDbContext _db;
    private readonly ISmsSender _smsSender;
    private readonly IFirebaseUserAdmin _firebaseUserAdmin;
    private readonly ICustomerRepository _customerRepository;
    private readonly ILogger<PhoneVerificationService> _logger;

    public PhoneVerificationService(
        ShaloTrackDbContext db,
        ISmsSender smsSender,
        IFirebaseUserAdmin firebaseUserAdmin,
        ICustomerRepository customerRepository,
        ILogger<PhoneVerificationService> logger)
    {
        _db = db;
        _smsSender = smsSender;
        _firebaseUserAdmin = firebaseUserAdmin;
        _customerRepository = customerRepository;
        _logger = logger;
    }

    public async Task<ApiResponse<object>> RequestOtpAsync(
        string rawPhoneNumber, string? requestIp, CancellationToken cancellationToken = default)
    {
        if (!TryNormalizePhoneNumber(rawPhoneNumber, out var phoneNumber))
        {
            return ApiResponse<object>.Fail(400, "Enter a valid Sri Lankan mobile number.");
        }

        var now = DateTime.UtcNow;

        // Resend cooldown -- blocks someone mashing "resend" from burning 5 SMS credits in 5 seconds.
        var mostRecent = await _db.PhoneVerifications
            .Where(p => p.PhoneNumber == phoneNumber)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (mostRecent is not null && (now - mostRecent.CreatedAt).TotalSeconds < ResendCooldownSeconds)
        {
            return ApiResponse<object>.Fail(429, "Please wait before requesting another code.");
        }

        // Hourly cap per number -- the IP-based rate limit policy on the controller stops a single
        // attacker; this stops the same number being targeted from many IPs (or many numbers from
        // one IP rotating through a VPN), and caps our own SMS spend per number either way.
        var recentCount = await _db.PhoneVerifications
            .Where(p => p.PhoneNumber == phoneNumber && p.CreatedAt > now.AddHours(-1))
            .CountAsync(cancellationToken);

        if (recentCount >= MaxRequestsPerHourPerNumber)
        {
            _logger.LogWarning("OTP request throttled for {Phone} -- {Count} requests in the last hour.", phoneNumber, recentCount);
            return ApiResponse<object>.Fail(429, "Too many codes requested for this number. Try again later.");
        }

        var code = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();
        var salt = RandomNumberGenerator.GetHexString(32);
        var codeHash = HashCode(code, salt);

        // Send BEFORE persisting: if text.lk fails, nothing is stored, so the cooldown/hourly-cap
        // checks above don't block an immediate, legitimate retry.
        try
        {
            await _smsSender.SendAsync(
                phoneNumber,
                $"Your Shalotrack verification code is {code}. It expires in {CodeExpiryMinutes} minutes. Do not share this code with anyone.",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send OTP SMS to {Phone}.", phoneNumber);
            return ApiResponse<object>.Fail(502, "Could not send verification code. Please try again shortly.");
        }

        _db.PhoneVerifications.Add(new PhoneVerification
        {
            PhoneVerificationId = Guid.NewGuid(),
            PhoneNumber = phoneNumber,
            CodeHash = codeHash,
            Salt = salt,
            AttemptCount = 0,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(CodeExpiryMinutes),
            RequestIp = requestIp
        });
        await _db.SaveChangesAsync(cancellationToken);

        return ApiResponse<object>.Ok(new { }, "Verification code sent.");
    }

    public async Task<ApiResponse<VerifyOtpResultDto>> VerifyOtpAsync(
        string rawPhoneNumber, string code, CancellationToken cancellationToken = default)
    {
        const string genericFailureMessage = "Invalid or expired code.";

        if (!TryNormalizePhoneNumber(rawPhoneNumber, out var phoneNumber))
        {
            return ApiResponse<VerifyOtpResultDto>.Fail(400, genericFailureMessage);
        }

        var now = DateTime.UtcNow;

        var verification = await _db.PhoneVerifications
            .Where(p => p.PhoneNumber == phoneNumber && p.ConsumedAt == null)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (verification is null)
        {
            _logger.LogWarning("OTP verify for {Phone}: no pending verification row.", phoneNumber);
            return ApiResponse<VerifyOtpResultDto>.Fail(400, genericFailureMessage);
        }

        if (verification.ExpiresAt < now)
        {
            _logger.LogWarning("OTP verify for {Phone}: code expired.", phoneNumber);
            return ApiResponse<VerifyOtpResultDto>.Fail(400, genericFailureMessage);
        }

        if (verification.AttemptCount >= MaxVerifyAttempts)
        {
            _logger.LogWarning("OTP verify for {Phone}: attempt limit reached.", phoneNumber);
            return ApiResponse<VerifyOtpResultDto>.Fail(400, genericFailureMessage);
        }

        var expectedHash = HashCode(code, verification.Salt);
        var matches = CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(expectedHash),
            Convert.FromHexString(verification.CodeHash));

        if (!matches)
        {
            verification.AttemptCount += 1;
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogWarning(
                "OTP verify for {Phone}: wrong code, attempt {Attempt}/{Max}.",
                phoneNumber, verification.AttemptCount, MaxVerifyAttempts);
            return ApiResponse<VerifyOtpResultDto>.Fail(400, genericFailureMessage);
        }

        verification.ConsumedAt = now;
        await _db.SaveChangesAsync(cancellationToken);

        var (uid, _) = await _firebaseUserAdmin.GetOrCreateUserByPhoneAsync(phoneNumber, cancellationToken);
        var customer = await _customerRepository.GetByFirebaseUidAsync(uid);
        var customToken = await _firebaseUserAdmin.CreateCustomTokenAsync(uid, cancellationToken);

        return ApiResponse<VerifyOtpResultDto>.Ok(new VerifyOtpResultDto
        {
            CustomToken = customToken,
            IsNewCustomer = customer is null
        }, "Phone number verified.");
    }

    private static string HashCode(string code, string salt)
    {
        var bytes = Encoding.UTF8.GetBytes($"{code}:{salt}");
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    /// <summary>
    /// Accepts "0771234567", "94771234567", "+94771234567" (with stray spaces/dashes) and
    /// normalizes to E.164 ("+94771234567"). Sri Lankan mobile numbers only: 9 significant
    /// digits after the country code, starting with 7.
    /// </summary>
    private static bool TryNormalizePhoneNumber(string? raw, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        var digits = new string(raw.Where(char.IsDigit).ToArray());

        string national; // 9 digits, no leading 0/94, e.g. "771234567"
        if (digits.Length == 10 && digits.StartsWith('0'))
        {
            national = digits[1..];
        }
        else if (digits.Length == 11 && digits.StartsWith("94"))
        {
            national = digits[2..];
        }
        else if (digits.Length == 9)
        {
            national = digits;
        }
        else
        {
            return false;
        }

        if (national.Length != 9 || national[0] != '7') return false;

        normalized = "+94" + national;
        return true;
    }
}
