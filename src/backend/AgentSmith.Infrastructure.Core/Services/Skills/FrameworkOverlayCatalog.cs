using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services.Skills;

/// <summary>
/// 2026-10-03-cf20c: the framework overlays one catalog's principles directory ships
/// (frameworks/&lt;slug&gt;.md). Lists them for detection and renders the applied ones for
/// composition. A file it cannot read whole is logged and applies nothing — a missing overlay
/// costs rules, a half-read one would mandate the wrong ones.
/// </summary>
internal sealed class FrameworkOverlayCatalog(string principlesDir, ILogger logger)
{
    private string FrameworksDir => Path.Combine(principlesDir, "frameworks");

    public IReadOnlyList<FrameworkOverlay> List()
    {
        if (!Directory.Exists(FrameworksDir)) return [];
        var overlays = new List<FrameworkOverlay>();
        foreach (var file in Directory.GetFiles(FrameworksDir, "*.md").Order(StringComparer.Ordinal))
        {
            var slug = Path.GetFileNameWithoutExtension(file);
            if (Read(slug, text => FrameworkOverlaySection.Detection(slug, text)) is { } overlay)
                overlays.Add(overlay);
        }
        return overlays;
    }

    /// <summary>
    /// The rendered overlays for <paramref name="languageSlug"/>, ordinal by slug, with the
    /// slugs that actually rendered — a requested slug the catalog cannot render is left out.
    /// </summary>
    public (IReadOnlyList<string> Applied, IReadOnlyList<string> Sections) Render(
        IReadOnlyList<string> overlaySlugs, string languageSlug)
    {
        var applied = new List<string>();
        var sections = new List<string>();
        foreach (var slug in overlaySlugs.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            if (Read(slug, text => FrameworkOverlaySection.Render(slug, text, languageSlug)) is not { } section)
                continue;
            applied.Add(slug);
            sections.Add(section);
        }
        return (applied, sections);
    }

    private T? Read<T>(string slug, Func<string, T?> parse) where T : class
    {
        var path = Path.Combine(FrameworksDir, $"{slug}.md");
        try
        {
            if (slug.Length == 0 || slug.IndexOfAny(['/', '\\', '.']) >= 0 || !File.Exists(path))
            {
                logger.LogWarning("Framework overlay '{Slug}' has no file at {Path} — not applied", slug, path);
                return null;
            }
            var parsed = parse(File.ReadAllText(path));
            if (parsed is null)
                logger.LogWarning("Framework overlay {Path} is malformed — not applied", path);
            return parsed;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException
                                       or YamlDotNet.Core.YamlException)
        {
            logger.LogWarning(ex, "Framework overlay {Path} could not be read — not applied", path);
            return null;
        }
    }
}
