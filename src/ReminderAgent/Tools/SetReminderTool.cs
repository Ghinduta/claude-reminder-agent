using System.Text.Json;
using Anthropic.Models.Messages;
using ReminderAgent.Reminders;

namespace ReminderAgent.Tools;

/// <summary>
/// set_reminder: the one tool with a side effect. It saves the reminder to the local JSON file.
/// </summary>
public sealed class SetReminderTool(ReminderStore store, TimeProvider timeProvider) : IAgentTool
{
    public Tool Definition { get; } = new()
    {
        Name = "set_reminder",
        Description =
            "Saves a reminder for the user. Call it only once the exact due date and time are known; " +
            "use get_current_datetime and add_duration_to_datetime first to work them out. " +
            "The due datetime must be in the future. Returns the saved reminder with its id.",
        InputSchema = new()
        {
            Properties = new Dictionary<string, JsonElement>
            {
                ["content"] = JsonSerializer.SerializeToElement(new
                {
                    type = "string",
                    description = "What to remind the user about, e.g. \"Doctor's appointment\".",
                }),
                ["due_at"] = JsonSerializer.SerializeToElement(new
                {
                    type = "string",
                    description = "When to remind the user, in ISO 8601, e.g. \"2026-11-19T09:00:00+02:00\".",
                }),
            },
            Required = ["content", "due_at"],
        },
    };

    public string Execute(IReadOnlyDictionary<string, JsonElement> input)
    {
        string content = input.GetRequiredString("content");
        DateTimeOffset dueAt = input.GetRequiredDateTime("due_at");
        DateTimeOffset now = timeProvider.GetLocalNow();

        if (dueAt <= now)
        {
            throw new ArgumentException(
                $"'due_at' {dueAt.ToIsoString()} is not in the future (now is {now.ToIsoString()}).");
        }

        var reminder = new Reminder(Guid.NewGuid(), content.Trim(), dueAt, now);
        store.Add(reminder);

        return JsonSerializer.Serialize(new
        {
            status = "saved",
            id = reminder.Id,
            content = reminder.Content,
            due_at = reminder.DueAt.ToIsoString(),
            day_of_week = reminder.DueAt.DayOfWeek.ToString(),
        });
    }
}
