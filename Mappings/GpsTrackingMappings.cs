using System.Linq.Expressions;
using ShaloTrack_API.DTOs.GpsTracking;
using ShaloTrack_API.Enums;
using ShaloTrack_API.Models;

namespace ShaloTrack_API.Mappings;

public static class GpsTrackingMappings
{
    public static readonly Expression<Func<GpsTracking, GpsTrackingResponseDto>>
        ToResponseDto =
        tracking => new GpsTrackingResponseDto
        {
            TrackingId = tracking.TrackingId,

            DeviceId = tracking.DeviceId,

            ImeiNumber = tracking.Device.ImeiNumber,

            VehicleId = tracking.Device.DeviceAssignments
                .Where(a => a.Status == AssignmentStatus.Active)
                .Select(a => a.VehicleId)
                .FirstOrDefault(),

            VehicleNumber = tracking.Device.DeviceAssignments
                .Where(a => a.Status == AssignmentStatus.Active)
                .Select(a => a.Vehicle.VehicleNumber)
                .FirstOrDefault() ?? string.Empty,

            Latitude = tracking.Latitude,

            Longitude = tracking.Longitude,

            Altitude = tracking.Altitude,

            Speed = tracking.Speed,

            Heading = tracking.Heading,

            Satellites = tracking.Satellites,

            GpsAccuracy = tracking.GpsAccuracy,

            EventTime = tracking.EventTime
        };
}