using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Auth;
using ShaloTrack_API.Constants;
using ShaloTrack_API.Data;
using ShaloTrack_API.DTOs.Customer;
using ShaloTrack_API.Models;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

/// <summary>
/// Changes the sign-in phone number of the signed-in customer.
///
/// Why this is strict: the phone number is the account's ONLY identity proof (email verification was removed on
/// 2 Nov 2026). Whoever controls the number controls the account and its vehicles. So:
///   1. the sign-in must be recent (same 10-minute rule as account deletion): a stolen or left-open session
///      cannot move the number;
///   2. a code is texted to the NEW number and must be entered: proves the person holds the new SIM;
///   3. the code is hashed, single use, expires in 5 minutes, locks after 5 wrong guesses, and is bound to this
///      customer AND this number (a login code cannot be reused here, nor this code to log in);
///   4. volume limits per customer and per target number stop the endpoint being used to spam a stranger's phone
///      or to burn SMS credit;
///   5. one successful change per 24 hours;
///   6. Firebase holds phone numbers unique and enforces it atomically, so the move happens THERE first; the
///      customer row follows, and if that fails Firebase is put back;
///   7. afterwards every refresh token is revoked (a thief's session dies with the old number) and the OLD number
///      is texted so the real owner notices.
///
/// Every wrong/expired/used code answers with the same words. A number that belongs to somebody else is answered
/// exactly like a success (no SMS is sent and the code can never verify): the endpoint must not tell a caller
/// which numbers are registered.
/// </summary>
public class PhoneChangeService : IPhoneChangeService
{
    private const int CodeExpiryMinutes = 5;
    private const int MaxVerifyAttempts = 5;
    private const int ResendCooldownSeconds = 45;
    private const int MaxRequestsPerHourPerCustomer = 5;
    private const int MaxRequestsPerHourPerNumber = 3;
    private static readonly TimeSpan MinTimeBetweenChanges = TimeSpan.FromHours(24);

    private const string GenericCodeFailure = "Invalid or expired code.";

    private readonly ShaloTrackDbContext _db;
    private readonly ISmsSender _smsSender;
    private readonly IFirebaseUserAdmin _firebaseUserAdmin;
    private readonly ICurrentUser _currentUser;
    private readonly ILogger<PhoneChangeService> _logger;

    public PhoneChangeService(
        ShaloTrackDbContext db,
        ISmsSender smsSender,
        IFirebaseUserAdmin firebaseUserAdmin,
        ICurrentUser currentUser,
        ILogger<PhoneChangeService> logger)
    {
        _db = db;
        _smsSender = smsSender;
        _firebaseUserAdmin = firebaseUserAdmin;
        _currentUser = currentUser;
        _logger = logger;
    }

    // ---------------------------------------------------------------------------------------------
    // Step 1: request a code for the new number
    // ---------------------------------------------------------------------------------------------

    public async Task<ApiResponse<PhoneChangeRequestedDto>> RequestAsync(
        RequestPhoneChangeDto dto, string? requestIp, CancellationToken cancellationToken = default)
    {
        var customer = await FindCustomerAsync(cancellationToken);
        if (customer is null)
            return ApiResponse<PhoneChangeRequestedDto>.Fail((int)HttpStatusCode.Unauthorized, "Authentication required.");

        var reauth = RequireRecentSignIn<PhoneChangeRequestedDto>();
        if (reauth is not null) return reauth;

        if (!PhoneNumberNormalizer.TryNormalize(dto.PhoneNumber, out var newPhone))
            return ApiResponse<PhoneChangeRequestedDto>.Fail((int)HttpStatusCode.BadRequest, "Enter a valid Sri Lankan mobile number.");

        if (IsSameNumber(customer.PhoneNumber, newPhone))
            return ApiResponse<PhoneChangeRequestedDto>.Fail((int)HttpStatusCode.BadRequest, "That is already your number.");

        var now = DateTime.UtcNow;
        var customerId = customer.CustomerId;

        // One successful change per 24 hours.
        var changedRecently = await _db.PhoneChangeVerifications
            .AnyAsync(p => p.CustomerId == customerId && p.AppliedAt != null && p.AppliedAt > now - MinTimeBetweenChanges, cancellationToken);
        if (changedRecently)
        {
            return ApiResponse<PhoneChangeRequestedDto>.Fail(
                (int)HttpStatusCode.TooManyRequests, "You changed your number recently. Please try again tomorrow.");
        }

        // Resend cooldown, per customer.
        var mostRecent = await _db.PhoneChangeVerifications
            .Where(p => p.CustomerId == customerId)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (mostRecent is not null && (now - mostRecent.CreatedAt).TotalSeconds < ResendCooldownSeconds)
        {
            return ApiResponse<PhoneChangeRequestedDto>.Fail(
                (int)HttpStatusCode.TooManyRequests, "Please wait before requesting another code.");
        }

        // Hourly cap per customer, and per target number across ALL customers (SMS-bomb protection).
        var hourAgo = now.AddHours(-1);
        var perCustomer = await _db.PhoneChangeVerifications
            .CountAsync(p => p.CustomerId == customerId && p.CreatedAt > hourAgo, cancellationToken);
        var perNumber = await _db.PhoneChangeVerifications
            .CountAsync(p => p.NewPhoneNumber == newPhone && p.CreatedAt > hourAgo, cancellationToken);
        if (perCustomer >= MaxRequestsPerHourPerCustomer || perNumber >= MaxRequestsPerHourPerNumber)
        {
            _logger.LogWarning(
                "Phone change request throttled for customer {CustomerId} (target {Phone}): {PerCustomer} by this customer, {PerNumber} for this number in the last hour.",
                customerId, PhoneNumberNormalizer.Mask(newPhone), perCustomer, perNumber);
            return ApiResponse<PhoneChangeRequestedDto>.Fail(
                (int)HttpStatusCode.TooManyRequests, "Too many codes requested. Try again later.");
        }

        var salt = RandomNumberGenerator.GetHexString(32);

        // A number that already belongs to someone else: answer like a success and send nothing. A row is still stored,
        // with a random code nobody knows, so it behaves exactly like a live challenge (same counters, same answers):
        // the caller cannot tell the difference. Even a lucky guess cannot succeed: VerifyAsync checks "taken" again.
        if (await IsNumberTakenAsync(newPhone, customer, cancellationToken))
        {
            _logger.LogInformation(
                "Phone change by customer {CustomerId} refused silently: {Phone} belongs to another account.",
                customerId, PhoneNumberNormalizer.Mask(newPhone));

            _db.PhoneChangeVerifications.Add(new PhoneChangeVerification
            {
                PhoneChangeVerificationId = Guid.NewGuid(),
                CustomerId = customerId,
                NewPhoneNumber = newPhone,
                CodeHash = HashCode(RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString(), salt),
                Salt = salt,
                AttemptCount = 0,
                CreatedAt = now,
                ExpiresAt = now.AddMinutes(CodeExpiryMinutes),
                RequestIp = requestIp
            });
            await _db.SaveChangesAsync(cancellationToken);

            return ApiResponse<PhoneChangeRequestedDto>.Ok(
                new PhoneChangeRequestedDto { ResendAfterSeconds = ResendCooldownSeconds }, "Verification code sent.");
        }

        var code = RandomNumberGenerator.GetInt32(100_000, 1_000_000).ToString();

        // Send BEFORE saving: if the SMS provider fails nothing is stored, so the cooldown does not block a retry.
        try
        {
            await _smsSender.SendAsync(
                newPhone,
                $"Your Shalotrack code to change your phone number is {code}. It expires in {CodeExpiryMinutes} minutes. Do not share this code with anyone.",
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send the phone-change SMS to {Phone}.", PhoneNumberNormalizer.Mask(newPhone));
            return ApiResponse<PhoneChangeRequestedDto>.Fail(
                (int)HttpStatusCode.BadGateway, "Could not send the verification code. Please try again shortly.");
        }

        _db.PhoneChangeVerifications.Add(new PhoneChangeVerification
        {
            PhoneChangeVerificationId = Guid.NewGuid(),
            CustomerId = customerId,
            NewPhoneNumber = newPhone,
            CodeHash = HashCode(code, salt),
            Salt = salt,
            AttemptCount = 0,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(CodeExpiryMinutes),
            RequestIp = requestIp
        });
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Phone change code sent for customer {CustomerId} to {Phone}.", customerId, PhoneNumberNormalizer.Mask(newPhone));

        return ApiResponse<PhoneChangeRequestedDto>.Ok(
            new PhoneChangeRequestedDto { ResendAfterSeconds = ResendCooldownSeconds }, "Verification code sent.");
    }

    // ---------------------------------------------------------------------------------------------
    // Step 2: verify the code and move the number
    // ---------------------------------------------------------------------------------------------

    public async Task<ApiResponse<PhoneChangeResultDto>> VerifyAsync(
        VerifyPhoneChangeDto dto, CancellationToken cancellationToken = default)
    {
        var customer = await FindCustomerAsync(cancellationToken);
        if (customer is null)
            return ApiResponse<PhoneChangeResultDto>.Fail((int)HttpStatusCode.Unauthorized, "Authentication required.");

        var reauth = RequireRecentSignIn<PhoneChangeResultDto>();
        if (reauth is not null) return reauth;

        if (!PhoneNumberNormalizer.TryNormalize(dto.PhoneNumber, out var newPhone)
            || dto.Code is null || dto.Code.Length != 6 || !dto.Code.All(char.IsAsciiDigit))
        {
            return ApiResponse<PhoneChangeResultDto>.Fail((int)HttpStatusCode.BadRequest, GenericCodeFailure);
        }

        var now = DateTime.UtcNow;
        var customerId = customer.CustomerId;
        var uid = customer.FirebaseUid!;

        // AsTracking: AttemptCount and AppliedAt below are saved with SaveChanges (see FindCustomerAsync).
        var challenge = await _db.PhoneChangeVerifications
            .AsTracking()
            .Where(p => p.CustomerId == customerId && p.NewPhoneNumber == newPhone && p.ConsumedAt == null)
            .OrderByDescending(p => p.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (challenge is null || challenge.ExpiresAt < now)
        {
            _logger.LogWarning("Phone change verify for customer {CustomerId}: no live challenge for {Phone}.", customerId, PhoneNumberNormalizer.Mask(newPhone));
            return ApiResponse<PhoneChangeResultDto>.Fail((int)HttpStatusCode.BadRequest, GenericCodeFailure);
        }

        if (challenge.AttemptCount >= MaxVerifyAttempts)
        {
            _logger.LogWarning("Phone change verify for customer {CustomerId}: attempt limit reached.", customerId);
            return ApiResponse<PhoneChangeResultDto>.Fail(
                (int)HttpStatusCode.TooManyRequests, "Too many wrong codes. Request a new code.");
        }

        var matches = CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(HashCode(dto.Code, challenge.Salt)),
            Convert.FromHexString(challenge.CodeHash));

        if (!matches)
        {
            challenge.AttemptCount += 1;
            await _db.SaveChangesAsync(cancellationToken);
            _logger.LogWarning(
                "Phone change verify for customer {CustomerId}: wrong code, attempt {Attempt}/{Max}.",
                customerId, challenge.AttemptCount, MaxVerifyAttempts);
            return ApiResponse<PhoneChangeResultDto>.Fail((int)HttpStatusCode.BadRequest, GenericCodeFailure);
        }

        // Taken in the meantime (checked BEFORE the code is spent, so the customer can still try another number).
        if (await IsNumberTakenAsync(newPhone, customer, cancellationToken))
        {
            _logger.LogWarning("Phone change verify for customer {CustomerId}: {Phone} was taken meanwhile.", customerId, PhoneNumberNormalizer.Mask(newPhone));
            return ApiResponse<PhoneChangeResultDto>.Fail((int)HttpStatusCode.BadRequest, GenericCodeFailure);
        }

        // Spend the code. Only one concurrent request can flip ConsumedAt from null; the loser gets the generic failure.
        var challengeId = challenge.PhoneChangeVerificationId;
        var claimed = await _db.PhoneChangeVerifications
            .Where(p => p.PhoneChangeVerificationId == challengeId && p.ConsumedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ConsumedAt, (DateTime?)now), cancellationToken);
        if (claimed == 0)
            return ApiResponse<PhoneChangeResultDto>.Fail((int)HttpStatusCode.BadRequest, GenericCodeFailure);

        // Firebase first: it is the authority on uniqueness and what the login resolves by.
        var previousFirebasePhone = await _firebaseUserAdmin.GetPhoneNumberAsync(uid, cancellationToken);
        var moved = await _firebaseUserAdmin.SetPhoneNumberAsync(uid, newPhone, cancellationToken);
        if (!moved)
        {
            _logger.LogWarning("Phone change for customer {CustomerId}: Firebase refused {Phone} (already in use).", customerId, PhoneNumberNormalizer.Mask(newPhone));
            return ApiResponse<PhoneChangeResultDto>.Fail((int)HttpStatusCode.BadRequest, GenericCodeFailure);
        }

        var oldPhone = customer.PhoneNumber;
        try
        {
            customer.PhoneNumber = newPhone;
            customer.UpdatedAt = now;
            challenge.AppliedAt = now;
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Phone change for customer {CustomerId}: saving the customer failed after Firebase moved. Rolling Firebase back.", customerId);
            if (!string.IsNullOrEmpty(previousFirebasePhone))
            {
                try
                {
                    await _firebaseUserAdmin.SetPhoneNumberAsync(uid, previousFirebasePhone, CancellationToken.None);
                }
                catch (Exception rollbackEx)
                {
                    _logger.LogCritical(rollbackEx,
                        "Phone change for customer {CustomerId}: ROLLBACK FAILED. Firebase has {NewPhone}, the database has {OldPhone}. Fix by hand.",
                        customerId, PhoneNumberNormalizer.Mask(newPhone), PhoneNumberNormalizer.Mask(oldPhone));
                }
            }
            return ApiResponse<PhoneChangeResultDto>.Fail(
                (int)HttpStatusCode.InternalServerError, "Could not change your number. Please try again.");
        }

        // From here the change is done. Everything below is best effort and must never undo or fail it.

        // Any other open challenge of this customer is void now.
        try
        {
            await _db.PhoneChangeVerifications
                .Where(p => p.CustomerId == customerId && p.ConsumedAt == null)
                .ExecuteUpdateAsync(s => s.SetProperty(p => p.ConsumedAt, (DateTime?)now), CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Phone change for customer {CustomerId}: could not void the other open challenges.", customerId);
        }

        // Sign out everywhere: a thief's session dies with the old number. (Existing ID tokens live out their hour.)
        try
        {
            await _firebaseUserAdmin.RevokeSessionsAsync(uid, CancellationToken.None);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Phone change for customer {CustomerId}: could not revoke sessions.", customerId);
        }

        // Tell the OLD number, so the real owner notices an account takeover.
        if (PhoneNumberNormalizer.TryNormalize(oldPhone, out var oldE164))
        {
            try
            {
                await _smsSender.SendAsync(
                    oldE164,
                    $"Your Shalotrack sign-in number was changed to {PhoneNumberNormalizer.Mask(newPhone)}. If this was not you, contact Shalotrack support immediately.",
                    CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Phone change for customer {CustomerId}: could not notify the old number {OldPhone}.", customerId, PhoneNumberNormalizer.Mask(oldE164));
            }
        }

        _logger.LogWarning(
            "AUDIT phone number changed: customer {CustomerId}, {OldPhone} -> {NewPhone}.",
            customerId, PhoneNumberNormalizer.Mask(oldPhone), PhoneNumberNormalizer.Mask(newPhone));

        return ApiResponse<PhoneChangeResultDto>.Ok(
            new PhoneChangeResultDto { PhoneNumber = newPhone }, "Your phone number was changed.");
    }

    // ---------------------------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------------------------

    private async Task<Customer?> FindCustomerAsync(CancellationToken cancellationToken)
    {
        var uid = _currentUser.FirebaseUid;
        if (string.IsNullOrEmpty(uid)) return null;

        // AsTracking: the context is NoTracking by default (DatabaseExtensions), and VerifyAsync mutates this entity
        // and calls SaveChanges. Without tracking that save writes nothing and the old number stays in the database.
        return await _db.Customers.AsTracking().FirstOrDefaultAsync(
            c => c.FirebaseUid == uid && c.DeletedAt == null && c.DeletionRequestedAt == null, cancellationToken);
    }

    /// <summary>Same 10-minute rule as account deletion: the Fleet/app token is minted at sign-in.</summary>
    private ApiResponse<T>? RequireRecentSignIn<T>()
    {
        var signedInAt = _currentUser.AuthTimeUtc;
        if (signedInAt is not null && DateTime.UtcNow - signedInAt.Value <= AccountDeletion.MaxSignInAge)
            return null;

        return ApiResponse<T>.Fail(
            (int)HttpStatusCode.Forbidden,
            $"For your security, please sign out and sign in again, then try within {(int)AccountDeletion.MaxSignInAge.TotalMinutes} minutes.",
            AccountDeletion.ReauthErrorCode);
    }

    private static bool IsSameNumber(string? stored, string newE164)
        => PhoneNumberNormalizer.TryNormalize(stored, out var storedE164) && storedE164 == newE164;

    /// <summary>Taken = another customer row carries it (in any stored spelling) or another Firebase account holds it.</summary>
    private async Task<bool> IsNumberTakenAsync(string newE164, Customer me, CancellationToken cancellationToken)
    {
        var variants = PhoneNumberNormalizer.StoredVariants(newE164);
        var myId = me.CustomerId;

        var usedByOtherCustomer = await _db.Customers.AnyAsync(
            c => c.CustomerId != myId && c.DeletedAt == null && variants.Contains(c.PhoneNumber), cancellationToken);
        if (usedByOtherCustomer) return true;

        var firebaseUid = await _firebaseUserAdmin.FindUidByPhoneNumberAsync(newE164, cancellationToken);
        return firebaseUid is not null && !string.Equals(firebaseUid, me.FirebaseUid, StringComparison.Ordinal);
    }

    private static string HashCode(string code, string salt)
        => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{code}:{salt}")));
}
