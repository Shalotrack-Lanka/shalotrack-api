using ShaloTrack_API.Models;

namespace ShaloTrack_API.Repositories.Interfaces;

public interface IGpsDeviceRepository
{
    Task<List<GpsDevice>> GetAllAsync();

    Task<GpsDevice?> GetByIdAsync(Guid deviceId);

    Task<GpsDevice?> GetByImeiAsync(string imei);

    Task<GpsDevice?> GetBySimNumberAsync(string simNumber);

    Task<bool> ExistsAsync(Guid deviceId);

    Task AddAsync(GpsDevice device);

    void Update(GpsDevice device);

    void Delete(GpsDevice device);
}