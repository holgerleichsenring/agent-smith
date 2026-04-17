using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Loads prompt templates from .md files in the config/prompts directory.
/// Templates are cached after first load.
/// </summary>
public sealed class FilePromptTemplateProvider : IPromptTemplateProvider
{
    private readonly string _promptsDirectory;
    private readonly ILogger<FilePromptTemplateProvider> _logger;
    private readonly Dictionary<string, string> _cache = new();

    public FilePromptTemplateProvider(
        string promptsDirectory,
        ILogger<FilePromptTemplateProvider> logger)
    {
        _promptsDirectory = promptsDirectory;
        _logger = logger;
    }

    public string Get(string templateName)
    {
        return GetOrDefault(templateName)
            ?? throw new FileNotFoundException(
                $"Prompt template '{templateName}' not found in {_promptsDirectory}. " +
                $"Expected file: {templateName}.md");
    }

    public string? GetOrDefault(string templateName)
    {
        if (_cache.TryGetValue(templateName, out var cached))
            return cached;

        var path = Path.Combine(_promptsDirectory, $"{templateName}.md");
        if (!File.Exists(path))
        {
            _logger.LogDebug("Prompt template not found: {Path}", path);
            return null;
        }

        var content = File.ReadAllText(path);

        // Strip YAML frontmatter if present
        content = StripFrontmatter(content);

        _cache[templateName] = content;
        _logger.LogDebug("Loaded prompt template: {Name} ({Chars} chars)", templateName, content.Length);
        return content;
    }

    internal static string StripFrontmatter(string content)
    {
        if (!content.StartsWith("---"))
            return content;

        var endIndex = content.IndexOf("\n---", 3);
        if (endIndex < 0)
            return content;

        return content[(endIndex + 4)..].TrimStart('\n', '\r');
    }
}
