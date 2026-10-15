using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Server.Security;

namespace AgentSmith.Server.Services.Access;

/// <summary>
/// 2026-10-15-4a4df: the one producer of project-binding findings, read by the access view
/// alone. The binding reader reports nothing because /api/identity reaches every caller and
/// must name no project; and "names no configured project" needs the configured names, which
/// only the access view reads.
/// <para>
/// A role's global-only permissions are no finding: the binding drops them by design and the
/// Projects pane shows them as ignored there.
/// </para>
/// </summary>
internal sealed class ProjectBindingFindings
{
    public IReadOnlyList<string> Of(ResolvedRoleMapping current, IReadOnlyCollection<string> projects)
    {
        ArgumentNullException.ThrowIfNull(current);
        var configured = projects.ToHashSet(ConfigNames.Comparer);
        return [.. current.Mapping.ProjectBindings
            .SelectMany(binding => Of(binding, current, configured))
            .Distinct(StringComparer.Ordinal)];
    }

    private static IEnumerable<string> Of(
        ProjectBinding binding, ResolvedRoleMapping current, HashSet<string> configured)
    {
        var project = binding.Project;
        if (string.IsNullOrWhiteSpace(project) || !configured.Contains(project))
            yield return $"The project binding for '{project}' names no configured project; it is kept "
                + "and applies if the project is configured again.";
        if (!current.Catalog.Permissions(binding.Roles).Any(Permissions.ProjectScopable.Contains))
            yield return $"The project binding for '{project}' grants nothing: none of its roles carries "
                + "a run, init or dialog permission.";
        foreach (var claim in binding.People.Select(p => p.Claim).Where(c => c != current.NameClaim))
            yield return $"A person in the project binding for '{project}' is written against the claim "
                + $"'{claim}', but callers are named by '{current.NameClaim}'; it grants nothing until "
                + "it is written again.";
    }
}
