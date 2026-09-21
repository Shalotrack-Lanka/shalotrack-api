namespace ShaloTrack_API.Repositories.Interfaces;

/// <summary>
/// RawPackets retention is a plain time-based sweep on ReceivedAt (the
/// gateway's own ingestion wall-clock) -- deliberately NOT scoped by device
/// or by any trip window. See RawPacketRetentionWorker for why: RawPackets
/// holds every protocol type (GPS pings, heartbeats, status, alarms,
/// logins, command acks/config), not just the pings that make up a "trip",
/// so there is no such thing as "this device's raw packets for this trip" --
/// only "this device's raw packets received in this time range."
///
/// The old per-device/per-range methods (CountByDeviceInRangeAsync /
/// DeleteByDeviceInRangeAsync) were removed 2026-09-22 -- they existed only
/// so TripPurgeService could delete RawPackets using a GpsTracking.EventTime
/// window, which silently assumed EventTime (device GPS clock) and
/// ReceivedAt (gateway ingestion clock) were interchangeable. They are not
/// guaranteed to be: a device that buffers while out of coverage and bursts
/// data later can have EventTime and ReceivedAt hours apart. See the
/// 2026-09-22 incident note in RawPacketRetentionWorker for the full story.
/// </summary>
public interface IRawPacketRepository
{
    Task<int> CountOlderThanAsync(DateTime cutoffUtc);

    /// <summary>
    /// Deletes up to <paramref name="batchSize"/> of the oldest rows with
    /// ReceivedAt &lt; <paramref name="cutoffUtc"/>, and returns how many
    /// were actually deleted. Callers loop this until it returns fewer than
    /// batchSize, so a large backlog (e.g. the first run after this worker
    /// ships) is purged in small bounded chunks instead of one long-held
    /// DELETE against the whole table.
    /// </summary>
    Task<int> DeleteOldestBatchAsync(DateTime cutoffUtc, int batchSize);
}