using System.Linq.Expressions;
using ShaloTrack_API.DTOs.CurrentLocation;
using ShaloTrack_API.Models;

namespace ShaloTrack_API.Mappings;

public static class CurrentLocationMappings
{
    public static readonly Expression<Func<CurrentLocation, CurrentLocationResponseDto>>
        ToResponseDto = location => new CurrentLocationResponseDto
        {
            VehicleId = location.VehicleId,
            VehicleNumber = location.Vehicle.VehicleNumber,
            DeviceId = location.DeviceId,
            ImeiNumber = location.Device.ImeiNumber,
            Latitude = location.Latitude,
            Longitude = location.Longitude,
            Speed = location.Speed,
            Heading = location.Heading,
            IgnitionStatus = location.IgnitionStatus,
            MovementStatus = location.MovementStatus,
            LastUpdate = location.LastUpdate
        };
}