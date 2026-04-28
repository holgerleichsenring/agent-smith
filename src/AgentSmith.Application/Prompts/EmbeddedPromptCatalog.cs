using System.Collections.Concurrent;
using System.Reflection;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Prompts;

/// <summary>
/// Resolves prompts from embedded .md resources under
/// <c>AgentSmith.Application/Prompts/Resources/</c>. An optional override source
/// may shadow the embedded content for developer iteration.
/// </summary>
public sealed class EmbeddedPromptCatalog(
    IPromptOverrideSource overrides,
    ILogger<EmbeddedPromptCatalog> logger) : IPromptCatalog
{
    private const string ResourcePrefix = "AgentSmith.Application.Prompts.Resources.";
    private static readonly Assembly ResourceAssembly = typeof(EmbeddedPromptCatalog).Assembly;
    private readonly ConcurrentDictionary<string, string> _cache = new(StringComparer.Ordinal);

    public string Get(string name)
    {
        if (overrides.TryGet(name, out var overrideContent))
        {
            logger.LogDebug("Prompt {Name} resolved from override source", name);
            return overrideContent;
        }

        return _cache.GetOrAdd(name, LoadEmbedded);
    }

    public string Render(string name, IReadOnlyDictionary<string, string> tokens)
    {
        var content = Get(name);
        foreach (var (key, value) in tokens)
        {
            content = content.Replace("{" + key + "}", value, StringComparison.Ordinal);
        }

        return content;
    }

    private static string LoadEmbedded(string name)
    {
        var resourceName = ResourcePrefix + name + ".md";
        using var stream = ResourceAssembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Prompt resource '{name}' not found at '{resourceName}'");
        using var reader = new StreamReader(stream);
        return StripFrontmatter(reader.ReadToEnd());
    }

    internal static string StripFrontmatter(string content)
    {
        if (!content.StartsWith("---", StringComparison.Ordinal))
            return content;

        var endIndex = content.IndexOf("\n---", 3, StringComparison.Ordinal);
        if (endIndex < 0)
            return content;

        return content[(endIndex + 4)..].TrimStart('\n', '\r');
    }
}
