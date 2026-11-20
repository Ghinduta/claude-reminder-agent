using System.Text.Json;
using ReminderAgent.Agent;
using ReminderAgent.Tools;

namespace ReminderAgent.Ui;

/// <summary>All console input and output lives here.</summary>
public sealed class ConsoleView
{
    public void ShowWelcome() =>
        Console.WriteLine("Reminder assistant. Type a request, or 'exit' to quit.");

    public void ShowAgentMode(AgentMode mode) => Console.WriteLine($"[agent loop: {mode}]");

    public string? ReadUserInput()
    {
        Console.Write("\nyou> ");
        return Console.ReadLine();
    }

    public void ShowAssistantText(string text) => Console.WriteLine($"\nclaude> {text}");

    public void ShowToolCall(string toolName, IReadOnlyDictionary<string, JsonElement> input) =>
        Console.WriteLine($"  [tool] {toolName} {JsonSerializer.Serialize(input)}");

    public void ShowToolResult(ToolExecutionResult result) =>
        Console.WriteLine(result.IsError ? $"  [error] {result.Content}" : $"  [result] {result.Content}");

    public void ShowWebSearch(string query) => Console.WriteLine($"  [search] {query}");

    public void ShowWebSearchError(string errorCode) => Console.WriteLine($"  [search error] {errorCode}");

    public void ShowSources(IEnumerable<(string Title, string Url)> sources)
    {
        foreach ((string title, string url) in sources)
        {
            Console.WriteLine($"  [source] {title} - {url}");
        }
    }

    public void ShowNotice(string message) => Console.WriteLine($"\n[{message}]");

    public void ShowError(string message) => Console.Error.WriteLine($"\nerror: {message}");

    public void ShowMissingApiKey() =>
        Console.Error.WriteLine("""
            No API key found. Set one of:
              dotnet user-secrets set ANTHROPIC_API_KEY "sk-ant-..."   (run inside src/ReminderAgent)
              $env:ANTHROPIC_API_KEY = "sk-ant-..."                    (PowerShell, current session)
            """);
}
