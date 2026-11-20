using System.Text.Json;
using Anthropic.Models.Messages;

namespace ReminderAgent.Tools;

/// <summary>
/// One tool Claude can call: its definition (what Claude sees) plus the code that runs it.
/// </summary>
public interface IAgentTool
{
    /// <summary>The name Claude uses in tool_use blocks; must match the name in <see cref="Definition"/>.</summary>
    string Name { get; }

    /// <summary>
    /// What is sent to the API in the Tools list: either a custom <see cref="Tool"/> with our own JSON
    /// schema, or an Anthropic-defined tool (such as the text editor) whose schema is built into the model.
    /// </summary>
    ToolUnion Definition { get; }

    /// <summary>
    /// Runs the tool with the input Claude chose. The returned string becomes the tool_result content.
    /// Throw on bad input or failure; the agent loop turns the exception into an is_error result.
    /// </summary>
    string Execute(IReadOnlyDictionary<string, JsonElement> input);
}
