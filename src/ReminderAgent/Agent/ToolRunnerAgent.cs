using System.Text;
using System.Text.Json;
using Anthropic;
using Anthropic.Helpers.Beta;
using Anthropic.Models.Beta.Messages;
using ReminderAgent.Tools;
using ReminderAgent.Ui;

namespace ReminderAgent.Agent;

/// <summary>
/// The same agent as <see cref="ManualAgent"/>, but the SDK's BetaToolRunner runs the loop:
/// it sends the request, runs the tools Claude asks for, sends the tool_results back with the
/// matching IDs, and repeats until Claude stops. We only add the user message and show the replies.
/// </summary>
public sealed class ToolRunnerAgent : IAgent
{
    // Web search runs on Anthropic's servers, so it is a plain definition on the thread, not a runnable
    // tool: the runner sends it but never executes it. (runner.AddTools would instead add it mid-thread
    // as a tool_addition block, which needs the separate inline-tools beta.)
    private static readonly BetaToolUnion WebSearch =
        new BetaWebSearchTool20260209 { MaxUses = AgentSettings.WebSearchMaxUses };

    private readonly IReadOnlyList<IBetaRunnableTool> _tools;
    private readonly ConsoleView _view;

    // A message thread (beta) keeps the conversation on Anthropic's server, so each request sends
    // only the new messages. The SDK also keeps a local copy, and after each run the thread holds
    // the whole history, so the next request continues the same conversation.
    private BetaMessageThread _thread;

    public ToolRunnerAgent(AnthropicClient client, IEnumerable<IAgentTool> tools, ToolExecutor executor, ConsoleView view)
    {
        _view = view;
        _tools = [.. tools.Select(tool => new RunnableToolAdapter(tool, executor, view))];
        _thread = client.Beta.Messages.Thread(new ThreadCreateParams
        {
            Model = AgentSettings.Model,
            MaxTokens = AgentSettings.MaxTokens,
            System = SystemPrompt.Text,
            OutputConfig = new BetaOutputConfig { Effort = Effort.Low },
            Tools = [WebSearch],
            Messages = [], // each turn's user message is appended in SendAsync
        });
    }

    public async Task SendAsync(string userInput)
    {
        // If a request fails, go back to the conversation as it was before this turn.
        BetaMessageThread beforeTurn = _thread.Fork();

        try
        {
            _thread.Append(new BetaUserMessageParam { Content = userInput });

            BetaMessage? lastMessage = null;
            int apiCalls = 0;

            // The runner only loops on our own tool_use calls. A pause_turn (the server paused a long
            // turn, e.g. during web searches) ends it, so we start it again on the same thread, which
            // now ends with the paused reply, to let Claude carry on. All calls share one round limit.
            do
            {
                // The runner yields one BetaMessage per API call; tools run between the calls.
                BetaToolRunner runner = _thread.ToolRunner(_tools, AgentSettings.MaxToolRounds - apiCalls);

                await foreach (BetaMessage message in runner)
                {
                    apiCalls++;
                    lastMessage = message;
                    ShowReply(message);
                }
            }
            while (lastMessage?.StopReason == "pause_turn" && apiCalls < AgentSettings.MaxToolRounds);

            ShowStopNotice(lastMessage, apiCalls);
        }
        catch
        {
            _thread = beforeTurn;
            throw;
        }
    }

    /// <summary>
    /// Shows the reply in order: text, the searches Claude ran, then the web pages its text cites.
    /// Cited text arrives split into several text blocks, so neighbouring text blocks are joined.
    /// </summary>
    private void ShowReply(BetaMessage message)
    {
        var text = new StringBuilder();
        var sources = new List<(string Title, string Url)>();

        foreach (BetaContentBlock block in message.Content)
        {
            if (block.TryPickText(out BetaTextBlock? textBlock))
            {
                text.Append(textBlock.Text);
                sources.AddRange((textBlock.Citations ?? [])
                    .Select(citation => citation.TryPickCitationsWebSearchResultLocation(
                        out BetaCitationsWebSearchResultLocation? web) ? (web.Title ?? web.Url, web.Url) : default)
                    .Where(source => source.Url is not null));
                continue;
            }

            FlushText(text);

            if (block.TryPickServerToolUse(out BetaServerToolUseBlock? search) &&
                search.Input.TryGetValue("query", out JsonElement query))
            {
                _view.ShowWebSearch(query.ToString());
            }
            else if (block.TryPickWebSearchToolResult(out BetaWebSearchToolResultBlock? result) &&
                     result.Content.TryPickError(out BetaWebSearchToolResultError? error))
            {
                _view.ShowWebSearchError(error.ErrorCode.ToString());
            }
        }

        FlushText(text);
        _view.ShowSources(sources.DistinctBy(source => source.Url));
    }

    private void FlushText(StringBuilder text)
    {
        if (text.Length > 0)
        {
            _view.ShowAssistantText(text.ToString());
            text.Clear();
        }
    }

    private void ShowStopNotice(BetaMessage? lastMessage, int apiCalls)
    {
        if (lastMessage is null)
        {
            return;
        }

        if ((lastMessage.StopReason == "tool_use" || lastMessage.StopReason == "pause_turn") && apiCalls >= AgentSettings.MaxToolRounds)
        {
            _view.ShowNotice($"stopped: more than {AgentSettings.MaxToolRounds} tool rounds, giving up on this request");
        }
        else if (lastMessage.StopReason == "max_tokens")
        {
            _view.ShowNotice("stopped: reply hit the max_tokens limit");
        }
        else if (lastMessage.StopReason == "refusal")
        {
            _view.ShowNotice("stopped: Claude declined this request");
        }
    }
}
