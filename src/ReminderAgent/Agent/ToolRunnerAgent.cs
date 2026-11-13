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
    private readonly IReadOnlyList<IBetaRunnableTool> _tools;
    private readonly ConsoleView _view;

    // A message thread (beta) keeps the conversation on Anthropic's server, so each request sends
    // only the new messages. The SDK also keeps a local copy, and after each run the thread holds
    // the whole history, so the next request continues the same conversation.
    private BetaMessageThread _thread;

    public ToolRunnerAgent(AnthropicClient client, ToolExecutor executor, ConsoleView view)
    {
        _view = view;
        _tools = [.. executor.Definitions.Select(definition => new RunnableToolAdapter(definition, executor, view))];
        _thread = client.Beta.Messages.Thread(new ThreadCreateParams
        {
            Model = AgentSettings.Model,
            MaxTokens = AgentSettings.MaxTokens,
            System = SystemPrompt.Text,
            OutputConfig = new BetaOutputConfig { Effort = Effort.Low },
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

            // The runner yields one BetaMessage per API call; tools run between the calls.
            BetaToolRunner runner = _thread.ToolRunner(_tools, AgentSettings.MaxToolRounds);
            BetaMessage? lastMessage = null;
            int apiCalls = 0;

            await foreach (BetaMessage message in runner)
            {
                apiCalls++;
                lastMessage = message;
                ShowText(message);
            }

            ShowStopNotice(lastMessage, apiCalls);
        }
        catch
        {
            _thread = beforeTurn;
            throw;
        }
    }

    private void ShowText(BetaMessage message)
    {
        foreach (BetaContentBlock block in message.Content)
        {
            if (block.TryPickText(out BetaTextBlock? text))
            {
                _view.ShowAssistantText(text.Text);
            }
        }
    }

    private void ShowStopNotice(BetaMessage? lastMessage, int apiCalls)
    {
        if (lastMessage is null)
        {
            return;
        }

        if (lastMessage.StopReason == "tool_use" && apiCalls >= AgentSettings.MaxToolRounds)
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
