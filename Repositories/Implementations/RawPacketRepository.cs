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

    public async Task<int> CountOlderThanAsync(DateTime cutoffUtc)
    {
        return await _context.RawPackets
            .Where(x => x.ReceivedAt < cutoffUtc)
            .CountAsync();
    }

    // Raw SQL, not ExecuteDeleteAsync(): EF Core's ExecuteDelete translation
    // doesn't support LIMIT/Take, and an unbounded DELETE over the full
    // backlog (first run after this worker ships, or after a long dry-run
    // period) would hold a single long transaction/lock and a large WAL
    // burst against a t3.micro Postgres instance. The subquery + LIMIT +
    // ORDER BY PacketId keeps each batch small, bounded, and deterministic
    // (oldest rows first).
    //
    // Safe against DeviceEvents referencing a deleted RawPacket: that FK is
    // optional (nullable RawPacketId) with ON DELETE SET NULL -- confirmed
    // against the migration snapshot, not assumed (same check done for the
    // old per-device delete this replaces).
    public async Task<int> DeleteOldestBatchAsync(DateTime cutoffUtc, int batchSize)
    {
        return await _context.Database.ExecuteSqlInterpolatedAsync($@"
            DELETE FROM ""RawPackets""
            WHERE ""PacketId"" IN (
                SELECT ""PacketId"" FROM ""RawPackets""
                WHERE ""ReceivedAt"" < {cutoffUtc}
                ORDER BY ""PacketId""
                LIMIT {batchSize}
            )");
    }
}