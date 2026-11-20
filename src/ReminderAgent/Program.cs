using Anthropic;
using Microsoft.Extensions.DependencyInjection;
using ReminderAgent.Agent;
using ReminderAgent.Configuration;
using ReminderAgent.Reminders;
using ReminderAgent.Tools;
using ReminderAgent.Ui;

var view = new ConsoleView();

AppSettings settings;
try
{
    settings = AppSettings.Load(args);
}
catch (InvalidOperationException ex)
{
    view.ShowError(ex.Message);
    return 1;
}

if (settings.ApiKey is null)
{
    view.ShowMissingApiKey();
    return 1;
}

ServiceCollection services = new();

services.AddSingleton(view);
services.AddSingleton(new AnthropicClient { ApiKey = settings.ApiKey });
services.AddSingleton(TimeProvider.System);
services.AddSingleton(_ => new ReminderStore("reminders.json"));

// Each tool is registered as IAgentTool; ToolExecutor receives all of them as IEnumerable<IAgentTool>.
services.AddSingleton<IAgentTool, GetCurrentDateTimeTool>();
services.AddSingleton<IAgentTool, AddDurationToDateTimeTool>();
services.AddSingleton<IAgentTool, SetReminderTool>();
services.AddSingleton<IAgentTool>(_ => new TextEditorTool("notes"));
services.AddSingleton<ToolExecutor>();

// The only place that knows which loop runs; everything else depends on IAgent.
switch (settings.AgentMode)
{
    case AgentMode.Manual:
        services.AddSingleton<IAgent, ManualAgent>();
        break;
    case AgentMode.ToolRunner:
        services.AddSingleton<IAgent, ToolRunnerAgent>();
        break;
}

services.AddSingleton<ConsoleChat>();

using ServiceProvider provider = services.BuildServiceProvider();

view.ShowAgentMode(settings.AgentMode);
await provider.GetRequiredService<ConsoleChat>().RunAsync();
return 0;
