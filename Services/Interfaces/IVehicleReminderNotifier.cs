namespace ShaloTrack_API.Services.Interfaces;

public sealed record VehicleReminderRunResult(int Sent, int Skipped);

public interface IVehicleReminderNotifier
{
    /// <summary>
    /// Sends the push for every reminder whose date is at a notice threshold at <paramref name="nowUtc"/>
    /// (30/14/7/1 days before, on the day, and once within 3 days after). Idempotent through
    /// VehicleReminderNotices. With <paramref name="dryRun"/> nothing is sent or recorded.
    /// </summary>
    Task<VehicleReminderRunResult> RunAsync(DateTime nowUtc, bool dryRun, CancellationToken cancellationToken = default);
}