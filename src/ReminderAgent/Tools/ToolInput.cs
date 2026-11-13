using System.Text.Json;

namespace ReminderAgent.Tools;

/// <summary>
/// Reads typed values from a tool_use input. Each method throws ArgumentException with a message
/// written for Claude, which the ToolExecutor sends back as an is_error tool_result.
/// </summary>
public static class ToolInput
{
    public static string? GetOptionalString(this IReadOnlyDictionary<string, JsonElement> input, string name)
    {
        if (!input.TryGetValue(name, out JsonElement element))
        {
            return null;
        }

        if (element.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(element.GetString()))
        {
            throw new ArgumentException($"'{name}' must be a non-empty string.");
        }

        return element.GetString()!;
    }

    public static string GetRequiredString(this IReadOnlyDictionary<string, JsonElement> input, string name) =>
        input.GetOptionalString(name) ?? throw new ArgumentException($"'{name}' is required.");

    public static int GetRequiredInt(this IReadOnlyDictionary<string, JsonElement> input, string name)
    {
        if (!input.TryGetValue(name, out JsonElement element))
        {
            throw new ArgumentException($"'{name}' is required.");
        }

        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt32(out int value))
        {
            throw new ArgumentException($"'{name}' must be a whole number.");
        }

        return value;
    }

    public static DateTimeOffset GetRequiredDateTime(this IReadOnlyDictionary<string, JsonElement> input, string name)
    {
        string value = input.GetRequiredString(name);

        if (!IsoDateTime.TryParse(value, out DateTimeOffset result))
        {
            throw new ArgumentException(
                $"'{name}' value '{value}' is not an ISO 8601 datetime. Use e.g. \"2026-11-19T09:00:00+02:00\".");
        }

        return result;
    }
}
