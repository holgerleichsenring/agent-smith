using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Enforces referential integrity on the editable catalog: a project may only
/// reference an agent / tracker / repos that exist in the catalog. This is the
/// same guarantee the DB schema enforces with FKs and the UI enforces by picking
/// refs from dropdowns — checked here so a broken wiring can never be persisted
/// through any store. Throws an aggregated <see cref="ConfigurationException"/>.
/// </summary>
public static class ConfigReferentialValidator
{
    /// <summary>Validate one project's refs against the catalog it will live in.</summary>
    public static void ValidateProject(ProjectEntity project, ConfigCatalog catalog)
    {
        var errors = new List<string>();

        if (!catalog.Agents.Any(a => a.Id == project.Agent))
            errors.Add($"project '{project.Id}' references unknown agent '{project.Agent}'");

        if (!catalog.Trackers.Any(t => t.Id == project.Tracker))
            errors.Add($"project '{project.Id}' references unknown tracker '{project.Tracker}'");

        var repoIds = catalog.Repos.Select(r => r.Id).ToHashSet();
        var connectionIds = catalog.Connections.Select(c => c.Id).ToHashSet();
        foreach (var repoRef in project.Repos)
        {
            // p0345b: a "connection/RepoName" (or connection/glob) ref resolves
            // against the CONNECTIONS catalog — valid iff the connection exists.
            // A plain ref keeps validating against the repos catalog. No ref form
            // is skipped: an unknown prefix is an error, never a silent pass.
            var slash = repoRef.IndexOf('/');
            if (slash > 0)
            {
                var connection = repoRef[..slash];
                if (!connectionIds.Contains(connection))
                    errors.Add(
                        $"project '{project.Id}' references unknown connection '{connection}' (repo ref '{repoRef}')");
                continue;
            }
            if (!repoIds.Contains(repoRef))
                errors.Add($"project '{project.Id}' references unknown repo '{repoRef}'");
        }

        if (errors.Count > 0)
        {
            var joined = string.Join("; ", errors);
            throw new ConfigurationException($"Referential integrity error(s): {joined}");
        }
    }

    /// <summary>Validate every project in a catalog — used before a full save/export.</summary>
    public static void ValidateCatalog(ConfigCatalog catalog)
    {
        foreach (var project in catalog.Projects)
            ValidateProject(project, catalog);
    }
}
