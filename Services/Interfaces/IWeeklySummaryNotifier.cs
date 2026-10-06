namespace ShaloTrack_API.Services.Interfaces;

public sealed record WeeklySummaryRunResult(int Sent, int Skipped, int Failed);

public interface IWeeklySummaryNotifier
{
    /// <summary>
    /// Sends the weekly summary push (last Monday to Sunday, Sri Lanka time) to customers who have
    /// not had it yet. Safe to call repeatedly: each customer's week is claimed atomically before
    /// sending. With <paramref name="dryRun"/> it only computes and logs a small sample (customer id
    /// and totals, no personal data), and sends and records nothing.
    /// </summary>
    Task<WeeklySummaryRunResult> RunAsync(DateTime nowUtc, bool dryRun, CancellationToken cancellationToken = default);
}