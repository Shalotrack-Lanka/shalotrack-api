using System.Collections.Concurrent;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

/// <summary>
/// Singleton by design -- must survive across requests to actually save
/// anything. See IArchivedTripCache for why this is safe to use as a
/// negative cache without reopening the Phase 3d partial-purge gap.
/// </summary>
public class ArchivedTripCache : IArchivedTripCache
{
    // Backstop only -- see interface doc. TripPurgeService's Invalidate()
    // call is what actually keeps this correct on the happy path.
    private static readonly TimeSpan SkipTtl = TimeSpan.FromMinutes(10);

    private readonly ConcurrentDictionary<Guid, DateTime> _skipUntil = new();

    public bool ShouldSkipS3(Guid deviceId) =>
        _skipUntil.TryGetValue(deviceId, out var until) && DateTime.UtcNow < until;

    public void MarkEmpty(Guid deviceId) =>
        _skipUntil[deviceId] = DateTime.UtcNow.Add(SkipTtl);

    public void Invalidate(Guid deviceId) =>
        _skipUntil.TryRemove(deviceId, out _);
}