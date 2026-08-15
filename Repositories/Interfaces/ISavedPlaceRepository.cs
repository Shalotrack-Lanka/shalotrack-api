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
}