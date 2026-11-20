using System.Text;
using System.Text.Json;
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

    private static readonly ToolUnion WebSearch =
        new(new WebSearchTool20260209 { MaxUses = AgentSettings.WebSearchMaxUses });

    // The whole conversation, sent in full on every request. The Messages API is stateless:
    // Claude only "remembers" what is in this list.
    private readonly List<MessageParam> _history = [];

    // web_search_20260209 filters results by running code in a server-side container. While a turn
    // still has tool calls open, every later request must name that container, or the API rejects it.
    // Kept for one user turn only, so an expired container is never reused.
    private string? _containerId;

    /// <summary>
    /// Handles one user request. If the API call fails, the turn is removed from history so
    /// it never holds a tool_use without its matching tool_result.
    /// </summary>
    public async Task SendAsync(string userInput)
    {
        int turnStart = _history.Count;
        _history.Add(new UserMessageParam { Content = userInput });
        _containerId = null;

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
            _containerId = response.Container?.ID ?? _containerId;

            // STEP 2: Append Claude's reply to history exactly as it produced it
            // (text, thinking, tool_use and web search blocks), and show it to the user.
            _history.Add(new AssistantMessageParam
            {
                Content = response.Content.Select(block => block.ToParam()).ToList(),
            });
            ShowReply(response);

            // STEP 3a: pause_turn means the server paused a long turn (e.g. during web searches).
            // Sending the conversation again, which now ends with the paused reply, lets Claude carry on.
            if (response.StopReason == "pause_turn")
            {
                continue;
            }

            // STEP 3b: Any other stop reason except tool_use means Claude is done with this request.
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

    private MessageCreateParams BuildRequest()
    {
        var request = new MessageCreateParams
        {
            Model = Model,
            MaxTokens = MaxTokens,
            System = SystemPrompt.Text,
            // Opus 5.5 always thinks (adaptively); effort is the dial. Its default is medium.
            // Date arithmetic is light work, so low keeps replies fast and cheap.
            OutputConfig = new OutputConfig { Effort = Effort.Low },
            // Our tools run here; web search runs on Anthropic's servers, so it is not in ToolExecutor.
            Tools = [.. tools.Definitions, WebSearch],
            Messages = _history,
        };

        return _containerId is null ? request : request with { Container = _containerId };
    }

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

    /// <summary>
    /// Shows the reply in order: text, the searches Claude ran, then the web pages its text cites.
    /// Cited text arrives split into several text blocks, so neighbouring text blocks are joined.
    /// </summary>
    private void ShowReply(Message response)
    {
        var text = new StringBuilder();
        var sources = new List<(string Title, string Url)>();

        foreach (ContentBlock block in response.Content)
        {
            if (block.TryPickText(out TextBlock? textBlock))
            {
                text.Append(textBlock.Text);
                sources.AddRange((textBlock.Citations ?? [])
                    .Select(citation => citation.TryPickCitationsWebSearchResultLocation(
                        out CitationsWebSearchResultLocation? web) ? (web.Title ?? web.Url, web.Url) : default)
                    .Where(source => source.Url is not null));
                continue;
            }

            FlushText(text);

            if (block.TryPickServerToolUse(out ServerToolUseBlock? search) &&
                search.Input.TryGetValue("query", out JsonElement query))
            {
                view.ShowWebSearch(query.ToString());
            }
            else if (block.TryPickWebSearchToolResult(out WebSearchToolResultBlock? result) &&
                     result.Content.TryPickError(out WebSearchToolResultError? error))
            {
                view.ShowWebSearchError(error.ErrorCode.ToString());
            }
        }

        FlushText(text);
        view.ShowSources(sources.DistinctBy(source => source.Url));
    }

    private void FlushText(StringBuilder text)
    {
        if (text.Length > 0)
        {
            view.ShowAssistantText(text.ToString());
            text.Clear();
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
