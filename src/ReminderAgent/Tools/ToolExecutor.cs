using System.Text.Json;
using Anthropic.Models.Messages;

namespace ReminderAgent.Tools;

public sealed record ToolExecutionResult(string Content, bool IsError);

/// <summary>
/// Looks up tools by the name Claude uses and runs them. Never throws: unknown tools and
/// tool exceptions become an error result that is sent back to Claude.
/// </summary>
public sealed class ToolExecutor(IEnumerable<IAgentTool> tools)
{
    private readonly Dictionary<string, IAgentTool> _toolsByName = tools.ToDictionary(t => t.Name);

    public IEnumerable<ToolUnion> Definitions => _toolsByName.Values.Select(t => t.Definition);

    public ToolExecutionResult Execute(string toolName, IReadOnlyDictionary<string, JsonElement> input)
    {
        if (!_toolsByName.TryGetValue(toolName, out IAgentTool? tool))
        {
            return new ToolExecutionResult($"Error: unknown tool '{toolName}'.", IsError: true);
        }

        try
        {
            return new ToolExecutionResult(tool.Execute(input), IsError: false);
        }
        catch (Exception ex)
        {
            return new ToolExecutionResult($"Error: {ex.Message}", IsError: true);
        }
    }
}
