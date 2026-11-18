using ShaloTrack_API.Models;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface ISavedPlaceRepository
{
    // Sorted by VisitCount descending -- "most frequented on top" is a
    // real, deliberate ordering requirement, not just a display nicety.
    Task<List<SavedPlace>> GetByCustomerAsync(Guid customerId);

    Task<SavedPlace?> GetByIdAsync(Guid placeId);

    Task AddAsync(SavedPlace place);
    void Remove(SavedPlace place);

    /// <summary>
    /// Counts one visit (VisitCount + 1 in the database, so two writers never lose a count). One UPDATE,
    /// runs immediately. (The context is NoTracking by default, so editing a loaded entity and calling SaveChanges writes nothing.)
    /// </summary>
    Task<int> RecordVisitAsync(Guid placeId, DateTime visitedAtUtc);
}
