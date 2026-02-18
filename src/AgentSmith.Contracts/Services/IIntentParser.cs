namespace AgentSmith.Contracts.Services;

/// <summary>
/// Parses free-form user input into a structured intent (ticket id + project name).
/// </summary>
public interface IIntentParser
{
    Task<ParsedIntent> ParseAsync(string userInput, CancellationToken cancellationToken = default);
}
