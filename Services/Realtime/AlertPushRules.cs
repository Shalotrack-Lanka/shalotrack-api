using ShaloTrack_API.Enums;
using ShaloTrack_API.Models;

namespace ShaloTrack_API.Services.Realtime;

/// <summary>
/// Whether an alert of this type on this vehicle should be PUSHED. The alert itself is always saved and listed
/// in the Alerts screen; this only decides the phone notification. Used by every alert push path
/// (LocationNotificationListener, DeviceOfflineNotifier) so the switches mean the same thing everywhere.
/// SOS is never mutable. Geofence alerts have their own per-geofence on/off and idle alerts their own
/// enable switch, so they always push here.
/// </summary>
public static class AlertPushRules
{
    public static bool ShouldPush(Vehicle vehicle, AlertType type) => type switch
    {
        AlertType.SOS => true,
        AlertType.PowerCut => vehicle.PushPowerCut,
        AlertType.IgnitionOn or AlertType.IgnitionOff => vehicle.PushIgnition,
        AlertType.Overspeed => vehicle.PushOverspeed,
        AlertType.LowBattery or AlertType.DeviceOffline => vehicle.PushTrackerHealth,
        _ => true,
    };
}
