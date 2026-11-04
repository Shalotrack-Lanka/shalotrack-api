using System.Text;
using System.Text.RegularExpressions;

namespace ShaloTrack_API.Services;

/// <summary>
/// What a customer's name and address may contain. These are the SAME rules the Fleet web portal enforces
/// (PersonName / Address in the Fleet repo); the API repeats them because the API is the only copy an attacker
/// or a buggy client cannot skip.
///
///   Name    : letters of any language (Sinhala and Tamil included, with their combining marks and the joiner
///             Sinhala conjuncts need), spaces, full stop, apostrophe, hyphen. Starts with a letter and holds at
///             least two letters. No digits, symbols, markup or emoji.
///   Address : letters of any language, digits 0-9, spaces, comma, slash, full stop, hyphen. At least one letter.
///
/// Both are tidied first (NFC, trimmed, runs of whitespace squeezed to one space), so " Nuwan   Silva " is the
/// same name as "Nuwan Silva". `\z` (not `$`) so a trailing line break can never slip through.
/// </summary>
public static class CustomerInputRules
{
    public const int MaxNameLength = 150;
    public const int MaxAddressLength = 300;

    public const string NameMessage =
        "Enter a real name: letters only (spaces, . ' and - are fine), no numbers or symbols.";

    public const string AddressMessage =
        "Use letters, numbers, spaces, commas (,), slashes (/), full stops (.) and hyphens (-) only.";

    // A match timeout keeps a hostile string from ever tying up a request thread.
    private static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(250);

    private static readonly Regex NameRegex = new(
        @"^(?=(?:\P{L}*\p{L}){2})\p{L}[\p{L}\p{M}‍ .'’\-]*\z",
        RegexOptions.CultureInvariant, Timeout);

    private static readonly Regex AddressRegex = new(
        @"^(?=.*\p{L})[\p{L}\p{M}‍0-9 ,.\/\-]+\z",
        RegexOptions.CultureInvariant, Timeout);

    private static readonly Regex Spaces = new(@"\s+", RegexOptions.CultureInvariant, Timeout);

    /// <summary>NFC, trim, squeeze whitespace. Null stays null.</summary>
    public static string? Tidy(string? input)
    {
        if (input is null) return null;
        return Spaces.Replace(input.Normalize(NormalizationForm.FormC).Trim(), " ");
    }

    public static bool IsValidName(string? name)
    {
        var n = Tidy(name);
        return !string.IsNullOrEmpty(n) && n.Length <= MaxNameLength && SafeIsMatch(NameRegex, n);
    }

    /// <summary>An empty address is allowed (it means "no address"); otherwise it must follow the rule.</summary>
    public static bool IsValidAddress(string? address)
    {
        var a = Tidy(address);
        if (string.IsNullOrEmpty(a)) return true;
        return a.Length <= MaxAddressLength && SafeIsMatch(AddressRegex, a);
    }

    /// <summary>The tidy form to store: an empty or blank address becomes null.</summary>
    public static string? TidyAddress(string? address)
    {
        var a = Tidy(address);
        return string.IsNullOrEmpty(a) ? null : a;
    }

    /// <summary>True when two spellings are the same once tidied (used to skip re-checking an unchanged field).</summary>
    public static bool SameText(string? a, string? b) =>
        string.Equals(Tidy(a) ?? string.Empty, Tidy(b) ?? string.Empty, StringComparison.Ordinal);

    private static bool SafeIsMatch(Regex regex, string value)
    {
        try { return regex.IsMatch(value); }
        catch (RegexMatchTimeoutException) { return false; }
    }
}