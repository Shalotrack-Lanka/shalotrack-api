namespace ShaloTrack_API.Repositories.Interfaces;

/// <summary>
/// NEW -- did not exist before Phase 3b. Every prior read of RawPackets in
/// this codebase went through the Python gateway directly, not the C# API --
/// this is the API's first reason to touch this table at all. Deliberately
/// minimal: only what the purge step needs, bounded by DeviceId + ReceivedAt
/// range, same pattern as IGpsTrackingRepository. No "get all" or unbounded
/// method here on purpose.
/// </summary>
public interface IRawPacketRepository
{
    Task<int> CountByDeviceInRangeAsync(Guid deviceId, DateTime from, DateTime to);
    Task<int> DeleteByDeviceInRangeAsync(Guid deviceId, DateTime from, DateTime to);
}