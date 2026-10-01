namespace ShaloTrack_API.Enums;

/// <summary>How long a device renewal extends the subscription. Maps 1:1 to the admin portal's models.</summary>
public enum RenewalDuration
{
    ThreeMonths = 0,
    SixMonths = 1,
    OneYear = 2,
    TwoYears = 3,
    ThreeYears = 4
}