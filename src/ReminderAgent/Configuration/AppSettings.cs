using Microsoft.Extensions.Configuration;
using ReminderAgent.Agent;

namespace ReminderAgent.Configuration;

/// <summary>
/// Settings read at startup. Later sources win: user-secrets, then environment variables,
/// then command-line arguments (e.g. <c>dotnet run -- --AGENT_MODE ToolRunner</c>).
/// </summary>
public sealed record AppSettings(string? ApiKey, AgentMode AgentMode)
{
    private const string ApiKeyName = "ANTHROPIC_API_KEY";
    private const string AgentModeName = "AGENT_MODE";

    public static AppSettings Load(string[] args)
    {
        IConfiguration config = new ConfigurationBuilder()
            .AddUserSecrets(typeof(AppSettings).Assembly)
            .AddEnvironmentVariables()
            .AddCommandLine(args)
            .Build();

        string? apiKey = config[ApiKeyName];

        return new AppSettings(
            string.IsNullOrWhiteSpace(apiKey) ? null : apiKey,
            ParseAgentMode(config[AgentModeName]));
    }

    private static AgentMode ParseAgentMode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return AgentMode.Manual;
        }

        if (Enum.TryParse(value, ignoreCase: true, out AgentMode mode) && Enum.IsDefined(mode))
        {
            return mode;
        }

        throw new InvalidOperationException(
            $"{AgentModeName} '{value}' is not valid. Use one of: {string.Join(", ", Enum.GetNames<AgentMode>())}.");
    }
}
