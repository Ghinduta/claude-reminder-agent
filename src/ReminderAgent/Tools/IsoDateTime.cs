using System.Globalization;

namespace ReminderAgent.Tools;

/// <summary>The one datetime format tools read and write, so Claude sees the same shape everywhere.</summary>
public static class IsoDateTime
{
    private const string Format = "yyyy-MM-dd'T'HH:mm:sszzz";

    public static string ToIsoString(this DateTimeOffset value) =>
        value.ToString(Format, CultureInfo.InvariantCulture);

    /// <summary>Parses any ISO 8601 datetime. A value without an offset is taken as local time.</summary>
    public static bool TryParse(string value, out DateTimeOffset result) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out result);
}
