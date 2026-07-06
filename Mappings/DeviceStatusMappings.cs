using System.Linq.Expressions;
using ShaloTrack_API.DTOs.DeviceStatus;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Models;

namespace ShaloTrack_API.Mappings;

public static class DeviceStatusMappings
{
    public static readonly Expression<Func<DeviceStatus, DeviceStatusResponseDto>>
        ToResponseDto = status => new DeviceStatusResponseDto
        {
            DeviceId = status.DeviceId,

            ImeiNumber = status.Device.ImeiNumber,

            VehicleId = status.Device.DeviceAssignments
                .Where(a => a.Status == AssignmentStatus.Active)
                .Select(a => (Guid?)a.VehicleId)
                .FirstOrDefault(),

            VehicleNumber = status.Device.DeviceAssignments
                .Where(a => a.Status == AssignmentStatus.Active)
                .Select(a => a.Vehicle.VehicleNumber)
                .FirstOrDefault(),

            IsOnline = status.IsOnline,

            LastHeartbeat = status.LastHeartbeat,

            LastSeen = status.LastSeen,

            GpsSignal = status.GpsSignal,

            BatteryLevel = status.BatteryLevel,

            IgnitionStatus = status.IgnitionStatus,

            MovementStatus = status.MovementStatus,

            PowerStatus = status.PowerStatus,

            UpdatedAt = status.UpdatedAt
        };
}