using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Core.Services.Skills;

/// <summary>
/// p0379: reads the authored principles templates from the resolved skill
/// catalog (principles/core.md + deltas/&lt;slug&gt;.md) and
/// composes them deterministically. Returns null when the catalog does not
/// ship the core template (older pins, unresolved catalog) so callers keep
/// the pre-p0379 behavior.
/// </summary>
public sealed class CatalogPrinciplesTemplateSource(
    ISkillsCatalogPath catalogPath,
    ILogger<CatalogPrinciplesTemplateSource> logger) : IPrinciplesTemplateSource
{
    // p0312a moved the templates to the catalog root: they are shared content, not a
    // skill, and the masters-only catalog has no category directory left to hold them.
    // Both paths are probed because the backend and the catalog version move independently
    // — the pin is operator configuration, so a 4.0.0 binary can face a 3.x catalog and a
    // 3.x binary a 4.0.0 one. Reading only the new path would make the principles vanish
    // from every run on an older pin, and this reader degrades SILENTLY (null = pre-p0379
    // behaviour), so the operator would never be told. Drop the legacy path once no pin
    // below 4.0.0 is in use.
    private static readonly string[] PrinciplesSubPaths =
    [
        "principles",              // catalog >= 4.0.0
        "skills/coding/principles" // catalog < 4.0.0
    ];

    public ComposedPrinciples? Compose(string languageSlug)
    {
        var principlesDir = TryResolvePrinciplesDirectory();
        if (principlesDir is null) return null;
        var corePath = Path.Combine(principlesDir, "core.md");
        if (!File.Exists(corePath))
        {
            logger.LogDebug("Catalog has no principles core at {Path} — pre-p0379 catalog", corePath);
            return null;
        }

        var slug = NormalizeSlug(languageSlug);
        var deltaPath = Path.Combine(principlesDir, "deltas", $"{slug}.md");
        var delta = File.Exists(deltaPath) ? File.ReadAllText(deltaPath) : null;
        var content = Render(File.ReadAllText(corePath), delta, slug);
        return new ComposedPrinciples(content, slug, DeltaApplied: delta is not null);
    }

    private string? TryResolvePrinciplesDirectory()
    {
        try
        {
            foreach (var subPath in PrinciplesSubPaths)
            {
                var dir = Path.Combine(catalogPath.Root, subPath);
                if (Directory.Exists(dir)) return dir;
            }

            return null;
        }
        catch (InvalidOperationException)
        {
            // Catalog not resolved yet (CLI tooling bypassing the server lifecycle).
            return null;
        }
    }

    // Free-form discovery slugs that mean the same delta file. Unknown slugs
    // pass through lowercased — the core then composes alone.
    private static string NormalizeSlug(string languageSlug)
    {
        var slug = (languageSlug ?? string.Empty).Trim().ToLowerInvariant();
        return slug switch
        {
            "c#" or "cs" or "dotnet" or "net" => "csharp",
            "ts" => "typescript",
            "" => "generic",
            _ => slug,
        };
    }

    // Deterministic by construction: pure concatenation of the template
    // files — no timestamps, no run identity. Two repos of the same stack
    // compose byte-identically and differ only by ratified project-specifics.
    private static string Render(string core, string? delta, string slug)
    {
        var deltaSection = delta is not null
            ? delta.TrimEnd()
            : $"_No language delta exists for '{slug}' yet — the universal core applies; "
              + "mechanisms follow the language's documented conventions._";
        return $"""
            # Coding Principles

            <!-- agentsmith:principles composed=core+{slug} status=proposed -->

            Transferred by init-project from the authored universal core plus the
            '{slug}' language delta. RATIFY by reviewing this file in the init pull
            request and merging it. Project-specific rules go under "Project
            Specifics" below; init-project re-runs preserve this file as-is.

            ---

            {core.TrimEnd()}

            ---

            {deltaSection}

            ---

            ## Project Specifics (ratified additions)

            _None yet. Rules appended here are project-authored and survive re-init._

            """;
    }
}
