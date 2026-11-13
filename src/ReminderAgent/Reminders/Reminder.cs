namespace ReminderAgent.Reminders;

public sealed record Reminder(Guid Id, string Content, DateTimeOffset DueAt, DateTimeOffset CreatedAt);
