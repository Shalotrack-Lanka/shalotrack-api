using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;
using ShaloTrack_API.Repositories.Interfaces;

namespace ShaloTrack_API.Repositories.Implementations;

public class RawPacketRepository : IRawPacketRepository
{
    private readonly ShaloTrackDbContext _context;

    public RawPacketRepository(ShaloTrackDbContext context)
    {
        _context = context;
    }

    public async Task<int> CountByDeviceInRangeAsync(Guid deviceId, DateTime from, DateTime to)
    {
        return await _context.RawPackets
            .Where(x => x.DeviceId == deviceId && x.ReceivedAt >= from && x.ReceivedAt <= to)
            .CountAsync();
    }

    // Safe against DeviceEvents referencing a deleted RawPacket: that
    // relationship is optional (nullable RawPacketId) with EF Core's default
    // ON DELETE SET NULL for optional FKs -- confirmed against the actual
    // migration snapshot before writing this, not assumed. Deleting a
    // RawPacket with DeviceEvents pointing to it nulls out that backlink,
    // it does not throw and does not cascade-delete the DeviceEvent itself.
    public async Task<int> DeleteByDeviceInRangeAsync(Guid deviceId, DateTime from, DateTime to)
    {
        return await _context.RawPackets
            .Where(x => x.DeviceId == deviceId && x.ReceivedAt >= from && x.ReceivedAt <= to)
            .ExecuteDeleteAsync();
    }
}