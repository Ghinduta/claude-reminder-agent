namespace ReminderAgent.Agent;

public enum AgentMode
{
    /// <summary>Our hand-written loop (<see cref="ManualAgent"/>).</summary>
    Manual,

    /// <summary>The SDK's BetaToolRunner (<see cref="ToolRunnerAgent"/>).</summary>
    ToolRunner,
}
