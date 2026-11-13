namespace ReminderAgent.Agent;

/// <summary>Takes one user request and runs the tool-use loop until Claude has answered it.</summary>
public interface IAgent
{
    Task SendAsync(string userInput);
}
