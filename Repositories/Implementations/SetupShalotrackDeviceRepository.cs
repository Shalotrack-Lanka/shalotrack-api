using Microsoft.EntityFrameworkCore;
using ShaloTrack_API.Data;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;

namespace ShaloTrack_API.Repositories.Implementations;

public class SetupShalotrackDeviceRepository : ISetupShalotrackDeviceRepository
{
    private readonly ShaloTrackDbContext _context;

    public SetupShalotrackDeviceRepository(ShaloTrackDbContext context)
    {
        _context = context;
    }

    public async Task<SetupShalotrackDevice?> GetByImeiAsync(string imei)
    {
        return await _context.SetupShalotrackDevices
            .FirstOrDefaultAsync(d => d.ImeiNumber == imei);
    }

    public async Task AddAsync(SetupShalotrackDevice device)
    {
        await _context.SetupShalotrackDevices.AddAsync(device);
    }

    public void Update(SetupShalotrackDevice device)
    {
        _context.SetupShalotrackDevices.Update(device);
    }
}