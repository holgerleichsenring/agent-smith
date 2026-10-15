using System.Security.Claims;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Server.Security;

/// <summary>
/// 2026-10-15-4a4da: the permissions a caller holds in ONE project, read out of the mapping's
/// project bindings. Built once per saved mapping, so each binding's roles are cut down to
/// their project-scopable permissions once rather than per request.
/// <para>
/// A caller matches a binding the way they match the global grants: a group value compared
/// ordinally after one leading slash is trimmed on both sides, or a person ref whose claim is
/// the configured name claim and whose value is <c>Identity.Name</c>, ordinally. The reader
/// reports nothing — a binding names a project, and its findings belong on the access view,
/// never on the identity answer every caller reads.
/// </para>
/// </summary>
internal sealed class ProjectBindingReader
{
    private readonly List<Bound> _bindings = [];

    public ProjectBindingReader(RoleMappingConfig mapping, string nameClaim, RoleCatalog catalog)
    {
        foreach (var binding in mapping.ProjectBindings)
        {
            if (string.IsNullOrWhiteSpace(binding.Project)) continue;
            _bindings.Add(new Bound(
                binding.Project,
                binding.Groups.Select(Normalize).ToHashSet(StringComparer.Ordinal),
                PeopleOn(binding, nameClaim),
                [.. catalog.Permissions(binding.Roles).Where(Security.Permissions.ProjectScopable.Contains)]));
        }
    }

    /// <summary>Project -> the sorted project-scopable permissions this caller holds there.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> Permissions(
        ClaimsPrincipal caller, IReadOnlyList<string> groups)
    {
        var name = caller.Identity?.Name;
        var normalized = groups.Select(Normalize).ToHashSet(StringComparer.Ordinal);
        var held = new Dictionary<string, HashSet<string>>(ConfigNames.Comparer);
        foreach (var bound in _bindings.Where(b => b.Permissions.Count > 0 && b.Matches(name, normalized)))
        {
            if (!held.TryGetValue(bound.Project, out var set))
                held[bound.Project] = set = new HashSet<string>(StringComparer.Ordinal);
            set.UnionWith(bound.Permissions);
        }

        return held.ToDictionary(
            e => e.Key,
            e => (IReadOnlyList<string>)[.. e.Value.Order(StringComparer.Ordinal)],
            ConfigNames.Comparer);
    }

    private static HashSet<string> PeopleOn(ProjectBinding binding, string nameClaim) =>
        binding.People
            .Where(p => string.Equals(p.Claim, nameClaim, StringComparison.Ordinal))
            .Select(p => p.Value)
            .ToHashSet(StringComparer.Ordinal);

    // The same one-character normalisation CallerRoleReader applies to a Keycloak group path.
    private static string Normalize(string group) => group.TrimStart('/');

    private sealed record Bound(
        string Project, HashSet<string> Groups, HashSet<string> People, IReadOnlyList<string> Permissions)
    {
        public bool Matches(string? name, HashSet<string> groups) =>
            (!string.IsNullOrEmpty(name) && People.Contains(name)) || Groups.Overlaps(groups);
    }
}
