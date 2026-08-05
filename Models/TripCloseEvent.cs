namespace ShaloTrack_API.Models;

/// <summary>
/// Enqueued by LocationNotificationListener the moment an IgnitionOff
/// transition is detected, from EITHER detection path (location_updates or
/// device_status_updates -- see LocationNotificationListener's class summary
/// for why both matter). Consumed by TripArchivalQueueWorker.
/// </summary>
public record TripCloseEvent(Guid DeviceId, Guid VehicleId, DateTime TripEndTime);