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
    // 2026-08-28-489a: the pre-4.0.0 location is gone. It was probed because a pin is
    // operator configuration and an older catalog could reach this reader; 4.7.0 is the
    // supported floor, so a catalog without principles here is below it, not laid out
    // differently.
    private const string SubPath = "principles";

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
            var dir = Path.Combine(catalogPath.Root, SubPath);
            return System.IO.Directory.Exists(dir) ? dir : null;
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
