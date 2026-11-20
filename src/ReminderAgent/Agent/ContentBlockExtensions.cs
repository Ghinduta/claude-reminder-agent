using System.Text.Json;
using Anthropic.Models.Messages;

namespace ReminderAgent.Agent;

public static class ContentBlockExtensions
{
    /// <summary>
    /// Converts a response block into the matching request block, so Claude's reply can be
    /// appended to the conversation history. The C# SDK has no built-in .ToParam().
    /// </summary>
    /// <remarks>
    /// The block is copied through its JSON instead of field by field. A reply must go back unchanged:
    /// thinking keeps its signature, cited text keeps its citations, and server-side tools (web search
    /// and the code it runs to filter results) keep every call and result. A field-by-field copy would
    /// silently drop any block type it does not list, which the API rejects on the next request.
    /// </remarks>
    public static ContentBlockParam ToParam(this ContentBlock block) =>
        JsonSerializer.Deserialize<ContentBlockParam>(JsonSerializer.SerializeToElement(block))
        ?? throw new InvalidOperationException("A response content block serialized to null.");
}
