using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Server.Security;

/// <summary>
/// p0503d: what a role NAME means, once an installation has had its say. The three
/// built-in bundles plus whatever the auth block adds, keyed case-insensitively — Entra
/// emits an app-role value as the operator capitalised it and Keycloak lowercases, so an
/// ordinal lookup would resolve <c>Admin</c> to nothing.
/// <para>
/// Custom roles are ADDITIVE, in both directions: a name that collides with a built-in
/// does not replace it, and a permission name the closed catalog does not contain is
/// dropped from the bundle and then reported. Filtering before reporting is what makes
/// "additive cannot grant what the catalog lacks" a mechanism rather than a claim.
/// </para>
/// </summary>
internal sealed class RoleCatalog
{
    private readonly Dictionary<string, IReadOnlyList<string>> _roles =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly List<string> _findings = [];

    public RoleCatalog(TokenAuthorityConfig auth)
    {
        foreach (var (name, bundle) in BuiltInRoles.All) _roles[name] = bundle;
        foreach (var (name, bundle) in auth.Roles) Add(name, bundle);
    }

    /// <summary>Configuration diagnoses, not caller diagnoses: they are the same for everyone.</summary>
    public IReadOnlyList<string> Findings => _findings;

    /// <summary>The union of the named roles' bundles. An unknown role name contributes nothing.</summary>
    public IReadOnlyList<string> Permissions(IEnumerable<string> roleNames) =>
        [.. roleNames
            .SelectMany(name => _roles.TryGetValue(name, out var bundle) ? bundle : [])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

    /// <summary>The role names this catalog knows, as an operator would have to spell them.</summary>
    public IReadOnlyList<string> Names => [.. _roles.Keys.Order(StringComparer.Ordinal)];

    private void Add(string name, IReadOnlyList<string> bundle)
    {
        if (BuiltInRoles.All.Keys.Contains(name, StringComparer.OrdinalIgnoreCase))
        {
            _findings.Add(
                $"The custom role '{name}' has the name of a built-in role, so it was "
                + "ignored. A built-in bundle is never replaced; pick another name.");
            return;
        }

        var unknown = bundle.Where(p => !Security.Permissions.All.Contains(p, StringComparer.Ordinal)).ToList();
        foreach (var permission in unknown)
            _findings.Add(
                $"The custom role '{name}' names the permission '{permission}', which is "
                + "not in the catalog, so it grants nothing. Check the spelling.");
        _roles[name] = [.. bundle.Except(unknown, StringComparer.Ordinal)];
    }
}
