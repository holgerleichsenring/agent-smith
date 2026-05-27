namespace AgentSmith.Contracts.Services;

/// <summary>
/// Resolves named LLM prompt resources to their final assembled string.
/// Implementations back this with embedded resources, optionally shadowed by
/// a developer override directory.
/// </summary>
public interface IPromptCatalog
{
    /// <summary>
    /// Returns the raw prompt content for the given name. Throws if no such
    /// resource exists.
    /// </summary>
    string Get(string name);

    /// <summary>
    /// Returns the prompt content with placeholders replaced. Token keys are
    /// matched as <c>{Key}</c> (single curly braces). Throws if no such resource exists.
    /// </summary>
    string Render(string name, IReadOnlyDictionary<string, string> tokens);
}
