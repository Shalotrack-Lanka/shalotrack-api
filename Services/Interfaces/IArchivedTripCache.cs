namespace ShaloTrack_API.Services.Interfaces;

/// <summary>
/// Per-(device, calendar-month) negative cache for the S3 archive merge in
/// GpsTrackingService (Phase 3d). Pure latency optimization -- see the
/// 2026-09-19 slow-load investigation: ReadPointsFromS3Async's S3 merge
/// fires unconditionally on every trip-history request, even for a month
/// that has never had a single row purged. Most of the fleet's history
/// stays in that state for a long time after GpsArchive:PurgeDryRun first
/// flips to false, so paying a ListObjectsV2 round-trip on every load, for
/// every month in the requested range, to find nothing, is pure waste.
///
/// INCIDENT NOTE (2026-09-19): the first version of this cache was keyed
/// per-device only, not per-device-per-month. That meant one request's
/// window coming back empty (e.g. the most recent 30-day chunk, before any
/// real purge had touched it) would suppress S3 entirely for that device
/// for the next 10 minutes -- including a LATER request for a DIFFERENT
/// 30-day chunk on the same device that genuinely had archived data.
/// Android fetches history in 30-day chunks, so this manifested as trip
/// history appearing to hard-stop at a fixed date. Root cause: the mark
/// was scoped too coarsely for what it was protecting. Fixed by keying
/// (and only ever setting the mark) per calendar month, matching the exact
/// granularity ReadPointsFromS3Async already lists by -- a mark can now
/// only ever suppress a request for the SAME month it was proven empty
/// for, never a different one.
///
/// Correctness is still protected two ways, not just a TTL:
///   1. A mark is only ever set after that month's S3 listing (the whole
///      month, not a request's from/to sub-range within it) genuinely came
///      back with zero objects.
///   2. TripPurgeService calls Invalidate(deviceId) immediately after a
///      REAL (non-dry-run) purge for that device, clearing ALL of that
///      device's cached months (not just the one touched -- we don't have
///      cheap visibility into exactly which month a purge's tripStart fell
///      in from here, and clearing the whole device is cheap and safe).
///      This is what keeps Phase 3d's actual correctness fix intact.
/// The TTL is a belt-and-suspenders backstop only, in case some other
/// future purge path is ever added that doesn't go through
/// TripPurgeService and therefore can't call Invalidate() itself.
/// </summary>
public interface IArchivedTripCache
{
    bool ShouldSkipMonth(Guid deviceId, int year, int month);
    void MarkMonthEmpty(Guid deviceId, int year, int month);
    void Invalidate(Guid deviceId);
}