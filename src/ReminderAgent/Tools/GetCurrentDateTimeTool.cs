using System.Globalization;
using System.Text.Json;
using Anthropic.Models.Messages;

namespace ReminderAgent.Tools;

/// <summary>
/// get_current_datetime: Claude has no clock, so "a week from Thursday" is meaningless
/// until it knows what today is. This tool gives it the user's local date and time.
/// </summary>
public sealed class GetCurrentDateTimeTool(TimeProvider timeProvider) : IAgentTool
{
    private const string DefaultFormat = "yyyy-MM-dd HH:mm:ss";

    public Tool Definition { get; } = new()
    {
        Name = "get_current_datetime",
        Description =
            "Returns the user's current local date and time, including the day of the week and the " +
            "UTC offset. Call this before working out any relative date or time (\"tomorrow\", " +
            "\"next Friday\", \"in 3 hours\"), because you do not otherwise know what today is. " +
            "The result always includes an ISO 8601 timestamp; 'date_format' only changes the " +
            "extra 'formatted' field.",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["date_format"] = JsonSerializer.SerializeToElement(new
                {
                    type = "string",
                    description =
                        ".NET custom date/time format string for the 'formatted' field, e.g. " +
                        "\"yyyy-MM-dd\", \"dddd, MMMM d, yyyy\" or \"HH:mm\". Defaults to \"" +
                        DefaultFormat + "\".",
                }),
            },
            Required = [],
        },
    };

    public string Execute(IReadOnlyDictionary<string, JsonElement> input)
    {
        string format = input.GetOptionalString("date_format") ?? DefaultFormat;
        DateTimeOffset now = timeProvider.GetLocalNow();

        string formatted;
        try
        {
            formatted = now.ToString(format, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            // Rethrow with a message Claude can act on (it will see this text with is_error = true).
            throw new ArgumentException(
                $"'{format}' is not a valid .NET date/time format string. Try e.g. \"yyyy-MM-dd HH:mm\".");
        }

        // JSON is easy for Claude to read reliably and lets us return several fields at once.
        return JsonSerializer.Serialize(new
        {
            iso = now.ToIsoString(),
            formatted,
            day_of_week = now.DayOfWeek.ToString(),
            utc_offset = now.ToString("zzz", CultureInfo.InvariantCulture),
            time_zone = timeProvider.LocalTimeZone.Id,
        });
    }
}
