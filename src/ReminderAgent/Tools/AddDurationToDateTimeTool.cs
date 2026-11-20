using System.Text.Json;
using Anthropic.Models.Messages;

namespace ReminderAgent.Tools;

/// <summary>
/// add_duration_to_datetime: date arithmetic done in code instead of in Claude's head, so
/// month lengths, leap years and daylight-saving changes are handled correctly.
/// </summary>
public sealed class AddDurationToDateTimeTool : IAgentTool
{
    private static readonly string[] Units = ["hours", "days", "weeks", "months"];

    private const string ToolName = "add_duration_to_datetime";

    public string Name => ToolName;

    public ToolUnion Definition { get; } = new Tool
    {
        Name = ToolName,
        Description =
            "Adds a duration to a datetime and returns the resulting datetime and its day of the week. " +
            "Use it for every date calculation instead of computing dates yourself, e.g. \"in 3 days\" " +
            "or \"a week from Thursday\" (first work out the date of Thursday, then add 1 week). " +
            "Use a negative amount to go back in time. Adding months keeps the day of the month where " +
            "possible and otherwise uses the month's last day (Jan 31 + 1 month = Feb 28 or 29).",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["datetime"] = JsonSerializer.SerializeToElement(new
                {
                    type = "string",
                    description = "Start datetime in ISO 8601, e.g. \"2026-11-12T09:00:00+02:00\".",
                }),
                ["amount"] = JsonSerializer.SerializeToElement(new
                {
                    type = "integer",
                    description = "How many units to add. Negative values subtract.",
                }),
                ["unit"] = JsonSerializer.SerializeToElement(new
                {
                    type = "string",
                    @enum = Units,
                    description = "Unit of the amount.",
                }),
            },
            Required = ["datetime", "amount", "unit"],
        },
    };

    public string Execute(IReadOnlyDictionary<string, JsonElement> input)
    {
        DateTimeOffset start = input.GetRequiredDateTime("datetime");
        int amount = input.GetRequiredInt("amount");
        string unit = input.GetRequiredString("unit");

        DateTimeOffset result = unit switch
        {
            "hours" => start.AddHours(amount),
            "days" => start.AddDays(amount),
            "weeks" => start.AddDays(amount * 7),
            "months" => start.AddMonths(amount),
            _ => throw new ArgumentException($"'unit' must be one of: {string.Join(", ", Units)}."),
        };

        return JsonSerializer.Serialize(new
        {
            iso = result.ToIsoString(),
            day_of_week = result.DayOfWeek.ToString(),
        });
    }
}
