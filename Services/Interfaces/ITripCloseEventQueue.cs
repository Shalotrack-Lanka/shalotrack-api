using ShaloTrack_API.Models;

namespace ShaloTrack_API.Services.Interfaces;

/// <summary>
/// Singleton in-memory queue between LocationNotificationListener (producer)
/// and TripArchivalQueueWorker (consumer). Exists so archival/purge work --
/// which can involve an S3 round trip and DB deletes -- never runs inline on
/// the Postgres NOTIFY handler thread and can't delay alert processing for
/// other devices.
///
/// KNOWN TRADEOFF: in-memory only, same as LocationNotificationListener's
/// alert-state cache. An API restart between enqueue and processing loses
/// queued events. Acceptable for now: this is not the only path a trip can
/// eventually get archived through (a future scheduled sweep could catch
/// anything missed), and losing an archival job occasionally is a cost/DB-
/// growth problem, not a data-loss or safety one -- the source
/// GpsTrackings/RawPackets rows are untouched until a purge actually
/// succeeds.
/// </summary>
public interface ITripCloseEventQueue
{
    void Enqueue(TripCloseEvent tripCloseEvent);
    IAsyncEnumerable<TripCloseEvent> ReadAllAsync(CancellationToken cancellationToken);
}