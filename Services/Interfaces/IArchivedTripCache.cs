namespace ShaloTrack_API.Services.Interfaces;

/// <summary>
/// Per-device negative cache for the S3 archive merge in GpsTrackingService
/// (Phase 3d). Pure latency optimization -- see the 2026-09-19 slow-load
/// investigation: GetTripsSummaryAsync's S3 merge fires unconditionally on
/// every trip-history request, even for a device that has never had a
/// single row purged. Most of the fleet stays in that state for a long time
/// after GpsArchive:PurgeDryRun first flips to false, so paying a
/// ListObjectsV2 round-trip on every single load, for every vehicle, to find
/// nothing, is pure waste.
///
/// Correctness is protected two ways, not just a TTL:
///   1. A "skip" mark is only ever set after ReadPointsFromS3Async's search
///      genuinely came back empty for that device's full requested prefix
///      set -- never assumed up front.
///   2. TripPurgeService calls Invalidate() immediately after a REAL
///      (non-dry-run) purge for that device, so the very next trip-history
///      read for it is guaranteed to check S3 again rather than waiting out
///      a TTL. This is what actually protects Phase 3d's correctness fix --
///      the gap it closed (partial-purge windows) stays closed.
/// The TTL is a belt-and-suspenders backstop only, in case some other
/// future purge path is ever added that doesn't go through
/// TripPurgeService and therefore can't call Invalidate() itself.
/// </summary>
public interface IArchivedTripCache
{
    bool ShouldSkipS3(Guid deviceId);
    void MarkEmpty(Guid deviceId);
    void Invalidate(Guid deviceId);
}