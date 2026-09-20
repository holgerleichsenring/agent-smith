using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Catalog;

/// <summary>
/// p0221: serves the resolved catalog's contents to the dashboard browser.
/// Resolves the catalog on demand (idempotent + cached via the resolver) so the
/// browser carries value even before the first run binds it, then reuses the
/// existing skill + vocabulary loaders. The loaded definitions are cached: the
/// loaders publish a SkillCatalogLoaded system event and rebuild index files,
/// so re-running them on every UI fetch would spam the system stream.
/// <para>
/// 2026-09-18-84be: origin, listing and body share ONE binding. The cache is keyed on
/// the binding the definitions were loaded under, so a re-resolve under this reader —
/// any run resolves, and the extractor and the overlay materializer rebuild into the
/// SAME absolute root — drops it instead of serving new text under an old phrase.
/// </para>
/// </summary>
public sealed class CatalogContentsReader(
    ISkillsCatalogResolver catalogResolver,
    ISkillLoader skillLoader,
    ISkillsCatalogPath catalogPath,
    TimeProvider clock,
    AgentSmithConfig config)
{
    private const string SkillsSubPath = "skills";
    private const string MasterRole = "master";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<RoleSkillDefinition>? _roles;
    private ConceptVocabulary? _vocabulary;
    private CatalogOrigin? _origin;

    public async Task<CatalogContentsResponse> GetContentsAsync(CancellationToken cancellationToken)
    {
        if (!await EnsureLoadedAsync(cancellationToken))
            return new CatalogContentsResponse(false, null, [], [], []);

        return new CatalogContentsResponse(
            Ready: true,
            Origin: _origin,
            Masters: Entries(_roles!.Where(IsMaster)),
            Skills: Entries(_roles!.Where(r => !IsMaster(r))),
            Concepts: Concepts(_vocabulary!));
    }

    public async Task<CatalogSkillBody?> GetSkillBodyAsync(string name, CancellationToken cancellationToken)
    {
        if (!await EnsureLoadedAsync(cancellationToken)) return null;

        var binding = _origin!.Phrase;
        var role = _roles!.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.Ordinal));
        if (role?.SkillDirectory is null) return null;

        var path = Path.Combine(role.SkillDirectory, "SKILL.md");
        if (!File.Exists(path)) return null;

        var markdown = await File.ReadAllTextAsync(path, cancellationToken);
        // The file name is stable across a re-resolve — the new tree is moved into the old
        // path — so text read across that swap is not the text the page's phrase describes.
        // It is not served; the next call re-loads and serves it under its own phrase.
        return IsCurrent(binding) ? new CatalogSkillBody(name, markdown) : null;
    }

    private async Task<bool> EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_roles is not null && IsCurrent(_origin?.Phrase)) return true;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_roles is not null && IsCurrent(_origin?.Phrase)) return true;
            var resolution = await catalogResolver.EnsureResolvedAsync(config.Skills, cancellationToken);
            var vocabulary = skillLoader.LoadVocabulary(SkillsSubPath);
            var roles = skillLoader.LoadRoleDefinitions(SkillsSubPath);
            _origin = Origin(resolution);
            _vocabulary = vocabulary;
            _roles = roles; // published LAST: it is the latch every fast path reads first
            return true;
        }
        catch
        {
            // Catalog source unavailable (network, missing cache, unresolved
            // path) — surface Ready=false rather than 500 the dashboard.
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    // The phrase comes off the resolution this call returned, never off the shared
    // singleton, which a concurrent resolve can re-point between the two lines.
    private CatalogOrigin Origin(CatalogResolution resolution) => new(
        resolution.Origin,
        resolution.Overlay is null ? null : config.Skills.Overlay,
        clock.GetUtcNow());

    private bool IsCurrent(string? binding) =>
        binding is not null && string.Equals(binding, catalogPath.Origin, StringComparison.Ordinal);

    private static bool IsMaster(RoleSkillDefinition role) =>
        string.Equals(role.Role, MasterRole, StringComparison.Ordinal);

    private static IReadOnlyList<CatalogEntry> Entries(IEnumerable<RoleSkillDefinition> roles) =>
        roles
            .Select(r => new CatalogEntry(r.Name, r.Role ?? "skill", r.Description))
            .OrderBy(e => e.Name, StringComparer.Ordinal)
            .ToArray();

    private static IReadOnlyList<CatalogConcept> Concepts(ConceptVocabulary vocabulary) =>
        vocabulary.Concepts.Values
            .Select(c => new CatalogConcept(c.Name, c.Type.ToString(), c.Description))
            .OrderBy(c => c.Name, StringComparer.Ordinal)
            .ToArray();
}
