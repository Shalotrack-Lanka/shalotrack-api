namespace ShaloTrack_API.Services.Interfaces;

/// <summary>Tunables for one offline sweep (read from the DeviceOffline: config section by the worker).</summary>
/// <param name="Threshold">No data for this long means the device is offline.</param>
/// <param name="MaxAge">Devices silent for longer than this are treated as abandoned and never alerted.</param>
/// <param name="MaxPerSweep">Upper bound of alerts created in one sweep (the rest follow on the next one).</param>
/// <param name="MassOutageMin">Safety valve, see DeviceOfflineNotifier: this many candidates AND more than half of
/// all recently active devices means the gateway or the database is down, not the devices.</param>
public sealed record DeviceOfflineOptions(
    TimeSpan Threshold,
    TimeSpan MaxAge,
    int MaxPerSweep,
    int MassOutageMin);

public sealed record DeviceOfflineRunResult(int Alerted, int Skipped, bool MassOutageSuspected);

public interface IDeviceOfflineNotifier
{
    /// <summary>
    /// Creates one DeviceOffline alert (and pushes it to the owner and the accepted viewers) for every device
    /// that is assigned to an active vehicle and has sent nothing for <see cref="DeviceOfflineOptions.Threshold"/>.
    /// Once per outage: the alert row itself is the marker, so a device is alerted again only after it has
    /// been seen again. With <paramref name="dryRun"/> nothing is saved or sent.
    /// </summary>
    Task<DeviceOfflineRunResult> RunAsync(
        DateTime nowUtc, DeviceOfflineOptions options, bool dryRun, CancellationToken cancellationToken = default);
}
