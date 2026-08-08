using ShaloTrack_API.Models;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface ISetupShalotrackDeviceRepository
{
    Task<SetupShalotrackDevice?> GetByImeiAsync(string imei);

    Task AddAsync(SetupShalotrackDevice device);

    void Update(SetupShalotrackDevice device);
}