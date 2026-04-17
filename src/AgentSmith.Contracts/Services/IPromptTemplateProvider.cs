namespace AgentSmith.Contracts.Services;

/// <summary>
/// Loads LLM prompt templates from the config/prompts directory.
/// Templates are .md files with optional YAML frontmatter.
/// </summary>
public interface IPromptTemplateProvider
{
    /// <summary>
    /// Returns the prompt template content for the given template name.
    /// The name corresponds to the file name without extension (e.g. "plan-consolidator-system").
    /// </summary>
    string Get(string templateName);

    /// <summary>
    /// Returns the prompt template content, or null if the template does not exist.
    /// </summary>
    string? GetOrDefault(string templateName);
}
