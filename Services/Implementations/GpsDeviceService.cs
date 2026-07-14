using System.Net;
using ShaloTrack_API.DTOs.GpsDevice;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Models;
using ShaloTrack_API.Repositories.Interfaces;
using ShaloTrack_API.Responses;
using ShaloTrack_API.Services.Interfaces;

namespace ShaloTrack_API.Services.Implementations;

public class GpsDeviceService : IGpsDeviceService
{
    private readonly IUnitOfWork _unitOfWork;

    public GpsDeviceService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ApiResponse<IReadOnlyList<GpsDeviceResponseDto>>> GetAllAsync()
    {
        var devices = await _unitOfWork.GpsDevices.GetAllAsync();

        var dtoList = devices
            .Select(ToDto)
            .ToList();

        return ApiResponse<IReadOnlyList<GpsDeviceResponseDto>>.Ok(
            dtoList,
            "GPS devices retrieved successfully."
        );
    }

    public async Task<ApiResponse<GpsDeviceResponseDto>> GetByIdAsync(Guid deviceId)
    {
        var device = await _unitOfWork.GpsDevices.GetByIdAsync(deviceId);

        if (device is null)
        {
            return ApiResponse<GpsDeviceResponseDto>.Fail(
                (int)HttpStatusCode.NotFound,
                "GPS device not found.",
                "The specified GPS device does not exist."
            );
        }

        return ApiResponse<GpsDeviceResponseDto>.Ok(
            ToDto(device),
            "GPS device retrieved successfully."
        );
    }

    public async Task<ApiResponse<GpsDeviceResponseDto>> CreateAsync(CreateGpsDeviceDto dto)
    {
        if (await _unitOfWork.GpsDevices.GetByImeiAsync(dto.ImeiNumber) is not null)
        {
            return ApiResponse<GpsDeviceResponseDto>.Fail(
                (int)HttpStatusCode.Conflict,
                "IMEI already exists.",
                "A GPS device with this IMEI already exists."
            );
        }

        if (!string.IsNullOrWhiteSpace(dto.SimNumber))
        {
            if (await _unitOfWork.GpsDevices.GetBySimNumberAsync(dto.SimNumber) is not null)
            {
                return ApiResponse<GpsDeviceResponseDto>.Fail(
                    (int)HttpStatusCode.Conflict,
                    "SIM number already exists.",
                    "SIM number must be unique."
                );
            }
        }

        var device = new GpsDevice
        {
            DeviceId = Guid.NewGuid(),
            ImeiNumber = dto.ImeiNumber,
            SimNumber = dto.SimNumber,
            DeviceModel = dto.DeviceModel,
            ProtocolType = dto.ProtocolType,
            NetworkProvider = dto.NetworkProvider,
            FirmwareVersion = dto.FirmwareVersion,
            ActivationStatus = ActivationStatus.Inactive,
            WarrantyExpiryDate = dto.WarrantyExpiryDate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _unitOfWork.GpsDevices.AddAsync(device);

        await _unitOfWork.SaveChangesAsync();

        device = await _unitOfWork.GpsDevices.GetByIdAsync(device.DeviceId)
                 ?? device;

        return ApiResponse<GpsDeviceResponseDto>.Ok(
            ToDto(device),
            "GPS device created successfully."
        );
    }

    public async Task<ApiResponse<GpsDeviceResponseDto>> UpdateAsync(
    Guid deviceId,
    UpdateGpsDeviceDto dto)
    {
        var device = await _unitOfWork.GpsDevices.GetByIdAsync(deviceId);

        if (device is null)
        {
            return ApiResponse<GpsDeviceResponseDto>.Fail(
                (int)HttpStatusCode.NotFound,
                "GPS device not found.",
                "The specified GPS device does not exist."
            );
        }

        if (!string.IsNullOrWhiteSpace(dto.SimNumber))
        {
            var existingDevice =
                await _unitOfWork.GpsDevices.GetBySimNumberAsync(dto.SimNumber);

            if (existingDevice != null &&
                existingDevice.DeviceId != deviceId)
            {
                return ApiResponse<GpsDeviceResponseDto>.Fail(
                    (int)HttpStatusCode.Conflict,
                    "SIM number already exists.",
                    "SIM number must be unique."
                );
            }
        }

        device.SimNumber = dto.SimNumber;
        device.DeviceModel = dto.DeviceModel;
        device.ProtocolType = dto.ProtocolType;
        device.NetworkProvider = dto.NetworkProvider;
        device.FirmwareVersion = dto.FirmwareVersion;
        device.ActivationStatus = dto.ActivationStatus;
        device.WarrantyExpiryDate = dto.WarrantyExpiryDate;
        device.InstalledAt = dto.InstalledAt;
        device.UpdatedAt = DateTime.UtcNow;

        _unitOfWork.GpsDevices.Update(device);

        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<GpsDeviceResponseDto>.Ok(
            ToDto(device),
            "GPS device updated successfully."
        );
    }

    public async Task<ApiResponse<string>> DeleteAsync(Guid deviceId)
    {
        var device = await _unitOfWork.GpsDevices.GetByIdAsync(deviceId);

        if (device is null)
        {
            return ApiResponse<string>.Fail(
                (int)HttpStatusCode.NotFound,
                "GPS device not found.",
                "The specified GPS device does not exist."
            );
        }

        _unitOfWork.GpsDevices.Delete(device);

        await _unitOfWork.SaveChangesAsync();

        return ApiResponse<string>.Ok(
            "GPS device deleted successfully.",
            "GPS device deleted successfully."
        );
    }

    public async Task<ApiResponse<DeviceLookupResponseDto>> LookupByImeiAsync(string imei)
    {
        if (string.IsNullOrWhiteSpace(imei))
        {
            return ApiResponse<DeviceLookupResponseDto>.Fail(
                (int)HttpStatusCode.BadRequest,
                "IMEI is required.",
                "Please enter a valid IMEI number."
            );
        }

        var device = await _unitOfWork.GpsDevices.GetByImeiAsync(imei);

        if (device is null)
        {
            return ApiResponse<DeviceLookupResponseDto>.Fail(
                (int)HttpStatusCode.NotFound,
                "Device not found.",
                "No device with this IMEI exists. Please check the number and try again."
            );
        }

        bool alreadyLinked = device.DeviceAssignments.Any(a => a.Status == AssignmentStatus.Active);
        if (alreadyLinked)
        {
            return ApiResponse<DeviceLookupResponseDto>.Fail(
                (int)HttpStatusCode.Conflict,
                "Device already linked.",
                "This device is already linked to another vehicle. Contact support if this is unexpected."
            );
        }

        return ApiResponse<DeviceLookupResponseDto>.Ok(
            new DeviceLookupResponseDto { DeviceId = device.DeviceId, ImeiNumber = device.ImeiNumber },
            "Device found and available to link."
        );
    }
    private static GpsDeviceResponseDto ToDto(GpsDevice device)
    {
        return new GpsDeviceResponseDto
        {
            DeviceId = device.DeviceId,
            ImeiNumber = device.ImeiNumber,
            SimNumber = device.SimNumber,
            DeviceModel = device.DeviceModel,
            ProtocolType = device.ProtocolType,
            NetworkProvider = device.NetworkProvider,
            FirmwareVersion = device.FirmwareVersion,
            ActivationStatus = device.ActivationStatus,
            WarrantyExpiryDate = device.WarrantyExpiryDate,
            InstalledAt = device.InstalledAt,
            IsAssigned = device.DeviceAssignments.Any(
                a => a.Status == AssignmentStatus.Active)
        };
    }
}