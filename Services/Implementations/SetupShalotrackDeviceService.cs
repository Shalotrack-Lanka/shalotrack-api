using ShaloTrack_API.DTOs.SetupShalotrackDevice;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class SetupShalotrackDeviceService : ISetupShalotrackDeviceService
{
    private readonly IUnitOfWork _unitOfWork;

    public SetupShalotrackDeviceService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<string>> UpsertAsync(SyncSetupShalotrackDeviceDto dto)
    {
        // Upsert by IMEI — the natural real-world unique key for a physical
        // device, more reliable than trusting Admin's local integer ID to
        // stay meaningful across two separate databases.
        var existing = await _unitOfWork.SetupShalotrackDevices.GetByImeiAsync(dto.ImeiNumber);

        if (existing is null)
        {
            var device = new SetupShalotrackDevice
            {
                Id = dto.Id,
                DeviceCategory = dto.DeviceCategory,
                ImeiNumber = dto.ImeiNumber,
                SimNumber = dto.SimNumber,
                Status = dto.Status,
                CancelReason = dto.CancelReason,
                CanceledDate = dto.CanceledDate,
                DealerId = dto.DealerId,
                DeviceTypeId = dto.DeviceTypeId,
                BoundAt = ResolveBoundAt(dto, null),
                CreatedAt = dto.CreatedAt,
                UpdatedAt = dto.UpdatedAt,
            };

            await _unitOfWork.SetupShalotrackDevices.AddAsync(device);
        }
        else
        {
            existing.DeviceCategory = dto.DeviceCategory;
            existing.SimNumber = dto.SimNumber;
            existing.Status = dto.Status;
            existing.CancelReason = dto.CancelReason;
            existing.CanceledDate = dto.CanceledDate;
            existing.DealerId = dto.DealerId;
            existing.DeviceTypeId = dto.DeviceTypeId;
            existing.BoundAt = ResolveBoundAt(dto, existing.BoundAt);
            existing.UpdatedAt = dto.UpdatedAt;

            _unitOfWork.SetupShalotrackDevices.Update(existing);
        }

        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<string>.Ok("Device synced successfully.", "Device synced successfully.");
    }

    // Old Admin payloads (IsBound == null) never change the stored value, so
    // deploying the API before Admin is safe. FirstSeenAt is deliberately not
    // touched here: it belongs to the gateway.
    private static DateTime? ResolveBoundAt(SyncSetupShalotrackDeviceDto dto, DateTime? current)
    {
        if (dto.IsBound is null) return current;
        if (dto.IsBound == false) return null;
        return dto.BoundAt?.ToUniversalTime() ?? current ?? DateTime.UtcNow;
    }
}