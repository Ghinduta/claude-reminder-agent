using System.Text.Json;
using Anthropic.Models.Messages;

namespace ReminderAgent.Tools;

/// <summary>
/// One tool Claude can call: its definition (what Claude sees) plus the code that runs it.
/// </summary>
public interface IAgentTool
{
    /// <summary>Name, description and JSON input schema sent to the API in the Tools list.</summary>
    Tool Definition { get; }

    /// <summary>
    /// Runs the tool with the input Claude chose. The returned string becomes the tool_result content.
    /// Throw on bad input or failure; the agent loop turns the exception into an is_error result.
    /// </summary>
    string Execute(IReadOnlyDictionary<string, JsonElement> input);
}
