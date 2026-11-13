using Anthropic.Models.Messages;

namespace ReminderAgent.Agent;

public static class ContentBlockExtensions
{
    /// <summary>
    /// Converts a response block into the matching request block, so Claude's reply can be
    /// appended to the conversation history. The C# SDK has no built-in .ToParam().
    /// Returns null for block types this app doesn't use (e.g. server-tool results).
    /// </summary>
    public static ContentBlockParam? ToParam(this ContentBlock block)
    {
        if (block.TryPickText(out TextBlock? text))
        {
            return new TextBlockParam { Text = text.Text };
        }

        if (block.TryPickThinking(out ThinkingBlock? thinking))
        {
            // Thinking must go back unchanged, signature included, or the API rejects the request.
            return new ThinkingBlockParam { Thinking = thinking.Thinking, Signature = thinking.Signature };
        }

        if (block.TryPickRedactedThinking(out RedactedThinkingBlock? redacted))
        {
            return new RedactedThinkingBlockParam { Data = redacted.Data };
        }

        if (block.TryPickToolUse(out ToolUseBlock? toolUse))
        {
            return new ToolUseBlockParam { ID = toolUse.ID, Name = toolUse.Name, Input = toolUse.Input };
        }

        return null;
    }
}
