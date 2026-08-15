using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;

namespace ShaloTrack_API.Repositories.Implementations;

public class SavedPlaceRepository : ISavedPlaceRepository
{
    private readonly ShaloTrackDbContext _context;

    public SavedPlaceRepository(ShaloTrackDbContext context)
    {
        _context = context;
    }

    public async Task<List<SavedPlace>> GetByCustomerAsync(Guid customerId)
    {
        return await _context.SavedPlaces
            .AsNoTracking()
            .Where(p => p.CustomerId == customerId)
            .OrderByDescending(p => p.VisitCount)
            .ThenByDescending(p => p.LastVisitedAt)
            .ToListAsync();
    }

    public async Task<SavedPlace?> GetByIdAsync(Guid placeId)
    {
        return await _context.SavedPlaces
            .FirstOrDefaultAsync(p => p.PlaceId == placeId);
    }

    public async Task AddAsync(SavedPlace place)
    {
        await _context.SavedPlaces.AddAsync(place);
    }

    public void Remove(SavedPlace place)
    {
        _context.SavedPlaces.Remove(place);
    }
}