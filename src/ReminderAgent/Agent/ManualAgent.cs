using Anthropic;
using Anthropic.Models.Messages;
using ReminderAgent.Tools;
using ReminderAgent.Ui;

namespace ReminderAgent.Agent;

/// <summary>
/// Holds the conversation and runs the hand-written agent loop:
/// send messages -> if Claude asks for tools, run them -> send the results back -> repeat.
/// Every step is visible here; compare with <see cref="ToolRunnerAgent"/>, where the SDK does them.
/// </summary>
public sealed class ManualAgent(AnthropicClient client, ToolExecutor tools, ConsoleView view) : IAgent
{
    private const string Model = AgentSettings.Model;
    private const int MaxTokens = AgentSettings.MaxTokens;
    private const int MaxToolRounds = AgentSettings.MaxToolRounds;

    // The whole conversation, sent in full on every request. The Messages API is stateless:
    // Claude only "remembers" what is in this list.
    private readonly List<MessageParam> _history = [];

    /// <summary>
    /// Handles one user request. If the API call fails, the turn is removed from history so
    /// it never holds a tool_use without its matching tool_result.
    /// </summary>
    public async Task SendAsync(string userInput)
    {
        int turnStart = _history.Count;
        _history.Add(new UserMessageParam { Content = userInput });

        try
        {
            await RunAgentLoopAsync();
        }
        catch
        {
            _history.RemoveRange(turnStart, _history.Count - turnStart);
            throw;
        }
    }

    private async Task RunAgentLoopAsync()
    {
        for (int round = 1; round <= MaxToolRounds; round++)
        {
            // STEP 1: Send the full conversation plus the tool definitions.
            // Claude decides on its own whether to answer directly or call a tool.
            Message response = await client.Messages.Create(BuildRequest());

            // STEP 2: Append Claude's reply to history exactly as it produced it
            // (text, thinking and tool_use blocks), and show any text to the user.
            _history.Add(new AssistantMessageParam
            {
                Content = response.Content.Select(block => block.ToParam()).OfType<ContentBlockParam>().ToList(),
            });
            ShowText(response);

            // STEP 3: Any stop reason other than tool_use means Claude is done with this request.
            if (response.StopReason != "tool_use")
            {
                ShowStopNotice(response);
                return;
            }

            // STEP 4: Claude is waiting for tool output. Run every tool_use block; each one gets
            // exactly one tool_result with the same ID. All results go back in ONE user message
            // (Claude may ask for several tools at once), then we loop to STEP 1.
            List<ContentBlockParam> toolResults = response.Content
                .Select(block => block.TryPickToolUse(out ToolUseBlock? toolUse) ? toolUse : null)
                .OfType<ToolUseBlock>()
                .Select(RunTool)
                .Select(result => (ContentBlockParam)result)
                .ToList();

            _history.Add(new UserMessageParam { Content = toolResults });
        }

        view.ShowNotice($"stopped: more than {MaxToolRounds} tool rounds, giving up on this request");
    }

    private MessageCreateParams BuildRequest() => new()
    {
        Model = Model,
        MaxTokens = MaxTokens,
        System = SystemPrompt.Text,
        // Opus 5.5 always thinks (adaptively); effort is the dial. Its default is medium.
        // Date arithmetic is light work, so low keeps replies fast and cheap.
        OutputConfig = new OutputConfig { Effort = Effort.Low },
        Tools = [.. tools.Definitions],
        Messages = _history,
    };

    private ToolResultBlockParam RunTool(ToolUseBlock toolUse)
    {
        view.ShowToolCall(toolUse.Name, toolUse.Input);
        ToolExecutionResult result = tools.Execute(toolUse.Name, toolUse.Input);
        view.ShowToolResult(result);

        // A failed tool is reported to Claude with is_error = true instead of crashing the app,
        // so Claude can retry with different input or explain the problem to the user.
        return new ToolResultBlockParam
        {
            ToolUseID = toolUse.ID,
            Content = result.Content,
            IsError = result.IsError,
        };
    }

    private void ShowText(Message response)
    {
        foreach (TextBlock text in response.Content.Select(block => block.Value).OfType<TextBlock>())
        {
            view.ShowAssistantText(text.Text);
        }
    }

    private void ShowStopNotice(Message response)
    {
        if (response.StopReason == "max_tokens")
        {
            view.ShowNotice("stopped: reply hit the max_tokens limit");
        }
        else if (response.StopReason == "refusal")
        {
            view.ShowNotice("stopped: Claude declined this request");
        }
    }
}
