using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services.Skills;

/// <summary>
/// 2026-08-28-7675: where a resolved catalog keeps its principles, and what that
/// catalog is called when a run has to report which one it read. Extracted from
/// <see cref="CatalogPrinciplesTemplateSource"/>, which composes them — locating a
/// directory and rendering a document are two responsibilities, and the composer
/// had grown past the length its own ratchet allows while carrying both.
/// </summary>
internal sealed class PrinciplesCatalogLayout(ISkillsCatalogPath catalogPath)
{
    /// <summary>What is reported when the catalog was never bound for this process.</summary>
    public const string Unresolved = "unresolved";

    // p0312a moved the templates to the catalog root: they are shared content, not a
    // skill, and the masters-only catalog has no category directory left to hold them.
    // Both paths are probed because the backend and the catalog version move independently
    // — the pin is operator configuration, so a 4.0.0 binary can face a 3.x catalog and a
    // 3.x binary a 4.0.0 one. Drop the legacy path once no pin below 4.0.0 is in use;
    // 2026-08-28-489a is that phase, and it is blocked on operator configuration.
    private static readonly string[] SubPaths =
    [
        "principles",              // catalog >= 4.0.0
        "skills/coding/principles" // catalog < 4.0.0
    ];

    /// <summary>The catalog in one operator-checkable phrase, or <see cref="Unresolved"/>.</summary>
    public string Origin
    {
        get
        {
            try { return catalogPath.Origin; }
            catch (InvalidOperationException) { return Unresolved; }
        }
    }

    /// <summary>The principles directory this catalog carries, or null when it carries none.</summary>
    public string? Directory()
    {
        try
        {
            foreach (var subPath in SubPaths)
            {
                var dir = Path.Combine(catalogPath.Root, subPath);
                if (System.IO.Directory.Exists(dir)) return dir;
            }

            return null;
        }
        catch (InvalidOperationException)
        {
            // Catalog not resolved yet (CLI tooling bypassing the server lifecycle). Silent
            // deliberately: it is reached on every such invocation, and a line printed every
            // time is a line nobody reads when it matters.
            return null;
        }
    }
}
