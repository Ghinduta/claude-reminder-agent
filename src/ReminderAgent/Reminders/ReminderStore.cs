using System.Text.Json;

namespace ReminderAgent.Reminders;

/// <summary>Keeps reminders in a local JSON file (a JSON array, rewritten on every add).</summary>
public sealed class ReminderStore(string filePath)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public string FilePath => Path.GetFullPath(filePath);

    public void Add(Reminder reminder)
    {
        List<Reminder> reminders = [.. Load(), reminder];
        File.WriteAllText(filePath, JsonSerializer.Serialize(reminders, JsonOptions));
    }

    private List<Reminder> Load()
    {
        if (!File.Exists(filePath))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<Reminder>>(File.ReadAllText(filePath), JsonOptions) ?? [];
    }
}
