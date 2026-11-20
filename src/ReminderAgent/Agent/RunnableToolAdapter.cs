using Anthropic.Helpers.Beta;
using Anthropic.Models.Beta.Messages;
using ReminderAgent.Tools;
using ReminderAgent.Ui;
using NonBeta = Anthropic.Models.Messages;

namespace ReminderAgent.Agent;

/// <summary>
/// Lets the SDK's tool runner call one of our tools. The runner only knows the beta types,
/// so this converts our tool definition to its beta twin and runs the call through ToolExecutor,
/// giving the same console output and error text as the manual loop.
/// </summary>
public sealed class RunnableToolAdapter(IAgentTool tool, ToolExecutor executor, ConsoleView view)
    : IBetaRunnableTool
{
    public string Name => tool.Name;

    public BetaToolUnion Definition { get; } = ToBeta(tool.Definition);

    public Task<BetaToolResultBlockParamContent> ExecuteAsync(BetaToolUseBlock toolUse, CancellationToken cancellationToken)
    {
        view.ShowToolCall(toolUse.Name, toolUse.Input);
        ToolExecutionResult result = executor.Execute(toolUse.Name, toolUse.Input);
        view.ShowToolResult(result);

        // The runner marks a tool_result as is_error when the tool throws, so a failure
        // reported by ToolExecutor is turned back into an exception here.
        if (result.IsError)
        {
            throw new InvalidOperationException(result.Content);
        }

        return Task.FromResult<BetaToolResultBlockParamContent>(result.Content);
    }

    // Only the two kinds of tool this app has are converted; a new kind fails at startup, not mid-chat.
    private static BetaToolUnion ToBeta(NonBeta.ToolUnion definition)
    {
        if (definition.TryPickTool(out NonBeta.Tool? custom))
        {
            return new BetaTool
            {
                Name = custom.Name,
                Description = custom.Description,
                InputSchema = new()
                {
                    Properties = custom.InputSchema.Properties,
                    Required = custom.InputSchema.Required,
                },
            };
        }

        if (definition.TryPickTextEditor20250728(out _))
        {
            return new BetaToolTextEditor20250728();
        }

        throw new NotSupportedException($"No beta conversion for tool definition {definition}.");
    }
}
