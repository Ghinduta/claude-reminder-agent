using Anthropic.Exceptions;
using ReminderAgent.Agent;

namespace ReminderAgent.Ui;

/// <summary>The read-eval-print loop: read a request, hand it to the assistant, repeat.</summary>
public sealed class ConsoleChat(IAgent agent, ConsoleView view)
{
    public async Task RunAsync()
    {
        view.ShowWelcome();

        while (true)
        {
            string? input = view.ReadUserInput();
            if (input is null || input.Trim().Equals("exit", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(input))
            {
                continue;
            }

            try
            {
                await agent.SendAsync(input);
            }
            catch (AnthropicApiException ex)
            {
                view.ShowError($"API call failed: {ex.Message}");
            }
        }
    }
}
