namespace ShaloTrack_API.Enums;

public enum AlertType
{
    IgnitionOn,
    IgnitionOff,
    Overspeed,
    PowerCut,
    LowBattery,
    DeviceOffline,
    SOS // NEW -- appended at the end (=6) deliberately, never inserted
        // earlier in the list. Existing alert rows already store these as
        // raw integers; inserting SOS anywhere but the end would silently
        // reassign what every existing stored value means.
}