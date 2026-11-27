using System.Collections.Concurrent;

namespace ShaloTrack_API.Hubs;

/// <summary>
/// Which customer connections on THIS API instance are in which vehicle's live-location group.
///
/// Why it exists: SignalR checks access once, when a connection joins a group. Before this, a viewer whose share
/// was revoked (or an owner whose vehicle moved to someone else) stayed in the group and kept receiving the
/// vehicle's live position for as long as their connection stayed open; a modified app could keep it open for
/// days. <see cref="ShaloTrack_API.Services.Realtime.LiveAccessSweeper"/> re-checks every entry here once a
/// minute and removes the ones that no longer have access.
///
/// Per instance on purpose: a connection lives on one instance, its group membership is local to that instance
/// (no backplane), and the pushes come from that instance's own LISTEN, so each instance only has to police its
/// own connections. Staff connections are not recorded: staff can see every vehicle.
/// </summary>
public sealed class LiveGroupRegistry
{
    /// <summary>One customer connection in one vehicle group.</summary>
    public readonly record struct Member(string ConnectionId, Guid VehicleId, string FirebaseUid);

    private sealed class Entry
    {
        public Entry(string firebaseUid) => FirebaseUid = firebaseUid;
        public string FirebaseUid { get; }
        public ConcurrentDictionary<Guid, byte> Vehicles { get; } = new();
    }

    private readonly ConcurrentDictionary<string, Entry> _connections = new(StringComparer.Ordinal);

    public void Add(string connectionId, Guid vehicleId, string firebaseUid)
    {
        var entry = _connections.GetOrAdd(connectionId, _ => new Entry(firebaseUid));
        entry.Vehicles[vehicleId] = 0;
    }

    public void Remove(string connectionId, Guid vehicleId)
    {
        if (!_connections.TryGetValue(connectionId, out var entry)) return;
        entry.Vehicles.TryRemove(vehicleId, out _);
        if (entry.Vehicles.IsEmpty) _connections.TryRemove(connectionId, out _);
    }

    public void RemoveConnection(string connectionId) => _connections.TryRemove(connectionId, out _);

    public bool IsEmpty => _connections.IsEmpty;

    public IReadOnlyList<Member> Snapshot()
    {
        var members = new List<Member>();
        foreach (var (connectionId, entry) in _connections)
        {
            foreach (var vehicleId in entry.Vehicles.Keys)
            {
                members.Add(new Member(connectionId, vehicleId, entry.FirebaseUid));
            }
        }
        return members;
    }
}
