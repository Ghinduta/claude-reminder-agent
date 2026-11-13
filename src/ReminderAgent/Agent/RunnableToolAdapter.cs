using Anthropic.Helpers.Beta;
using Anthropic.Models.Beta.Messages;
using ReminderAgent.Tools;
using ReminderAgent.Ui;
using NonBeta = Anthropic.Models.Messages;

namespace ReminderAgent.Agent;

/// <summary>
/// Lets the SDK's tool runner call one of our tools. The runner only knows the beta types,
/// so this converts our tool definition to a BetaTool and runs the call through ToolExecutor,
/// giving the same console output and error text as the manual loop.
/// </summary>
public sealed class RunnableToolAdapter(NonBeta.Tool definition, ToolExecutor executor, ConsoleView view)
    : IBetaRunnableTool
{
    public string Name => definition.Name;

    public BetaToolUnion Definition { get; } = new BetaTool
    {
        Name = definition.Name,
        Description = definition.Description,
        InputSchema = new()
        {
            Properties = definition.InputSchema.Properties,
            Required = definition.InputSchema.Required,
        },
    };

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
}
