namespace AgentSmith.Contracts.Services;

/// <summary>
/// Optional shadowing source for embedded prompts. When configured (e.g. via
/// AGENTSMITH_PROMPT_OVERRIDES), it lets a developer iterate on prompt content
/// without rebuilding. Production deployments leave this disabled.
/// </summary>
public interface IPromptOverrideSource
{
    /// <summary>
    /// Returns true and sets <paramref name="content"/> if an override exists for
    /// the given prompt name; otherwise returns false.
    /// </summary>
    bool TryGet(string name, out string content);
}
