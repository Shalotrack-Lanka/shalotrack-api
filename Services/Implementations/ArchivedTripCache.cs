using System.Collections.Concurrent;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

/// <summary>
/// Singleton by design -- must survive across requests to actually cache
/// anything. See IArchivedTripCache for why this is keyed per calendar
/// month (not per device) and how correctness is protected.
/// </summary>
public class ArchivedTripCache : IArchivedTripCache
{
    // Backstop only -- see interface doc. TripPurgeService's Invalidate()
    // call is what actually keeps this correct on the happy path.
    private static readonly TimeSpan SkipTtl = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<(Guid DeviceId, int Year, int Month), DateTime> _skipUntil = new();

    public bool ShouldSkipMonth(Guid deviceId, int year, int month) =>
        _skipUntil.TryGetValue((deviceId, year, month), out var until) && DateTime.UtcNow < until;

    public void MarkMonthEmpty(Guid deviceId, int year, int month) =>
        _skipUntil[(deviceId, year, month)] = DateTime.UtcNow.Add(SkipTtl);

    public void Invalidate(Guid deviceId)
    {
        // Clears every cached month for this device -- see interface doc
        // for why whole-device invalidation, not just the touched month.
        foreach (var key in _skipUntil.Keys)
        {
            if (key.DeviceId == deviceId)
            {
                _skipUntil.TryRemove(key, out _);
            }
        }
    }
}