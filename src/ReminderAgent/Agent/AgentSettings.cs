namespace ReminderAgent.Agent;

/// <summary>Request settings both agents share, so the two loops send equivalent requests.</summary>
public static class AgentSettings
{
    public const string Model = "claude-opus-5-5";
    public const int MaxTokens = 16000;

    // Safety net: stop if Claude keeps calling tools without ever finishing.
    public const int MaxToolRounds = 10;
}
