using System.Linq.Expressions;
using ShaloTrack_API.DTOs.DeviceEvent;
using ShaloTrack_API.Models;

namespace ShaloTrack_API.Mappings;

public static class DeviceEventMappings
{
    public static readonly Expression<Func<DeviceEvent, DeviceEventResponseDto>>
        ToResponseDto = deviceEvent => new DeviceEventResponseDto
        {
            EventId = deviceEvent.EventId,
            DeviceId = deviceEvent.DeviceId,
            VehicleId = deviceEvent.VehicleId,
            EventType = deviceEvent.EventType,
            Severity = deviceEvent.Severity,
            Latitude = deviceEvent.Latitude,
            Longitude = deviceEvent.Longitude,
            RawPacketId = deviceEvent.RawPacketId,
            Description = deviceEvent.Description,
            Metadata = deviceEvent.Metadata,
            CreatedAt = deviceEvent.CreatedAt
        };
}