using System.Collections.Concurrent;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services.Skills;

/// <summary>
/// p0313b: reads <c>references/&lt;slug&gt;.md</c> from the resolved skill catalog.
/// Same shape as <see cref="CatalogPrinciplesTemplateSource"/> — shared content at
/// the catalog root, not a skill — and cached per slug because a reference is
/// static for the life of a pinned catalog.
/// </summary>
public sealed class CatalogSkillReferenceSource(ISkillsCatalogPath catalogPath) : ISkillReferenceSource
{
    private const string ReferencesSubPath = "references";

    private readonly ConcurrentDictionary<string, string?> _cache = new(StringComparer.Ordinal);

    public string? TryRead(string slug) => _cache.GetOrAdd(slug, Read);

    private string? Read(string slug)
    {
        try
        {
            var path = Path.Combine(catalogPath.Root, ReferencesSubPath, $"{slug}.md");
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (InvalidOperationException)
        {
            // Catalog not resolved yet (CLI tooling bypassing the server lifecycle).
            // The citing master then fails loud at render rather than here — this
            // source only answers "do I have it", never "is that acceptable".
            return null;
        }
    }
}
