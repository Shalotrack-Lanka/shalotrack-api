namespace ShaloTrack_API.Services;

/// <summary>
/// Strict Sri Lankan mobile parsing for the phone-NUMBER-CHANGE flow. Stricter than the login OTP's private
/// parser on purpose (the login path is left exactly as it was): the raw text may only contain what a person
/// really types (digits 0-9, spaces, + ( ) . -). Letters, markup and other-script digits are rejected, not
/// silently dropped.
/// </summary>
public static class PhoneNumberNormalizer
{
    /// <summary>"0771234567", "077 123 4567", "+94 77 123 4567", "0094771234567", "94771234567", "771234567" -> "+94771234567".</summary>
    public static bool TryNormalize(string? raw, out string e164)
    {
        e164 = string.Empty;
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 25) return false;

        foreach (var c in raw)
        {
            var allowed = char.IsAsciiDigit(c) || c is ' ' or '+' or '(' or ')' or '.' or '-';
            if (!allowed) return false;
        }

        var digits = new string(raw.Where(char.IsAsciiDigit).ToArray());

        if (digits.StartsWith("0094")) digits = digits[4..];
        else if (digits.StartsWith("94") && digits.Length > 9) digits = digits[2..];
        if (digits.StartsWith('0')) digits = digits[1..];

        if (digits.Length != 9 || digits[0] != '7') return false;

        e164 = "+94" + digits;
        return true;
    }

    /// <summary>Every way this number may be stored on an old row: "+94771234567", "0771234567", "94771234567".</summary>
    public static string[] StoredVariants(string e164)
    {
        var national = e164.StartsWith("+94") ? e164[3..] : e164;
        return new[] { "+94" + national, "0" + national, "94" + national };
    }

    /// <summary>"+94771234567" -> "+94 77 *** 4567" for messages and logs. Never log a full number.</summary>
    public static string Mask(string? e164)
    {
        if (string.IsNullOrEmpty(e164) || e164.Length < 8) return "***";
        return e164[..^7] + " *** " + e164[^4..];
    }
}
