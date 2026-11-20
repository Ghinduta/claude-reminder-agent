namespace ReminderAgent.Agent;

/// <summary>Request settings both agents share, so the two loops send equivalent requests.</summary>
public static class AgentSettings
{
    public const string Model = "claude-opus-5-5";
    public const int MaxTokens = 16000;

    // Safety net: stop if Claude keeps calling tools without ever finishing.
    // Also bounds pause_turn continuations, since each one is another API call.
    public const int MaxToolRounds = 10;

    // Web search is billed per search on top of tokens, and every result page it returns stays in the
    // conversation as input tokens. Finding one event date rarely needs more than a couple of searches.
    public const int WebSearchMaxUses = 3;
}
