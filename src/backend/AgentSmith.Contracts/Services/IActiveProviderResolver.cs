namespace AgentSmith.Contracts.Services;

/// <summary>
/// Resolves the active provider name (claude, openai, azure-openai, gemini, ollama)
/// for skill loading. Empty / null disables provider-specific SKILL overrides; the
/// base SKILL.md is then always used.
/// </summary>
public interface IActiveProviderResolver
{
    string GetActiveProvider();
}
