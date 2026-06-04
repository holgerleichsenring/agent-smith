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
/// </summary>
public sealed class CatalogContentsReader(
    ISkillsCatalogResolver catalogResolver,
    ISkillLoader skillLoader,
    AgentSmithConfig config)
{
    private const string SkillsSubPath = "skills";
    private const string MasterRole = "master";

    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<RoleSkillDefinition>? _roles;
    private ConceptVocabulary? _vocabulary;

    public async Task<CatalogContentsResponse> GetContentsAsync(CancellationToken cancellationToken)
    {
        if (!await EnsureLoadedAsync(cancellationToken))
            return new CatalogContentsResponse(false, [], [], []);

        return new CatalogContentsResponse(
            Ready: true,
            Masters: Entries(_roles!.Where(IsMaster)),
            Skills: Entries(_roles!.Where(r => !IsMaster(r))),
            Concepts: Concepts(_vocabulary!));
    }

    public async Task<CatalogSkillBody?> GetSkillBodyAsync(string name, CancellationToken cancellationToken)
    {
        if (!await EnsureLoadedAsync(cancellationToken)) return null;

        var role = _roles!.FirstOrDefault(r => string.Equals(r.Name, name, StringComparison.Ordinal));
        if (role?.SkillDirectory is null) return null;

        var path = Path.Combine(role.SkillDirectory, "SKILL.md");
        if (!File.Exists(path)) return null;

        return new CatalogSkillBody(name, await File.ReadAllTextAsync(path, cancellationToken));
    }

    private async Task<bool> EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        if (_roles is not null) return true;
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_roles is not null) return true;
            await catalogResolver.EnsureResolvedAsync(config.Skills, cancellationToken);
            _roles = skillLoader.LoadRoleDefinitions(SkillsSubPath);
            _vocabulary = skillLoader.LoadVocabulary(SkillsSubPath);
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
