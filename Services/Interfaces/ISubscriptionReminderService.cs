namespace ShaloTrack_API.Services.Interfaces;

public sealed record SubscriptionReminderRunResult(int UpcomingSent, int ExpiredSent, int Skipped);

public interface ISubscriptionReminderService
{
    /// <summary>
    /// Sends the renewal reminders that are due at <paramref name="nowUtc"/> (14, 7 and 1 days
    /// before a subscription ends, plus one "subscription ended" notice after it lapses).
    /// Idempotent: reminders already recorded in SubscriptionReminderLogs are never sent again.
    /// With <paramref name="dryRun"/> nothing is sent and nothing is recorded; it only logs
    /// what would go out.
    /// </summary>
    Task<SubscriptionReminderRunResult> RunAsync(DateTime nowUtc, bool dryRun, CancellationToken cancellationToken = default);
}