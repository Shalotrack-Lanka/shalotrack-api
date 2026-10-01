using ShaloTrack_API.Enums;

namespace ShaloTrack_API.Extensions;

public static class RenewalEnumExtensions
{
    /// <summary>The exact subscription_model string the admin portal uses.</summary>
    public static string ToAdminModel(this RenewalDuration d) => d switch
    {
        RenewalDuration.ThreeMonths => "3 Months",
        RenewalDuration.SixMonths => "6 Months",
        RenewalDuration.OneYear => "1 Year",
        RenewalDuration.TwoYears => "2 Year",
        RenewalDuration.ThreeYears => "3 Year",
        _ => throw new ArgumentOutOfRangeException(nameof(d), d, "Unhandled renewal duration.")
    };

    /// <summary>Strict parse: names only. "2" or "99" (numeric enum values) are NOT accepted.</summary>
    public static bool TryParseStrict<TEnum>(string? raw, out TEnum value) where TEnum : struct, Enum
    {
        value = default;
        if (string.IsNullOrWhiteSpace(raw) || int.TryParse(raw, out _)) return false;
        return Enum.TryParse(raw.Trim(), ignoreCase: true, out value) && Enum.IsDefined(value);
    }
}