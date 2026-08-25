using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Enforces referential integrity on the editable catalog: a project may only
/// reference an agent / tracker / repos that exist in the catalog. This is the
/// same guarantee the DB schema enforces with FKs and the UI enforces by picking
/// refs from dropdowns — checked here so a broken wiring can never be persisted
/// through any store. Throws an aggregated <see cref="ConfigurationException"/>.
/// <para>
/// p0515: a reference is matched under <see cref="ConfigNames.Comparer"/>, the rule the
/// loader's catalogs are keyed by, and a reference matching MORE than one catalog entry is
/// rejected as well — the loader drops both halves of such a pair, so an editor that
/// accepted the reference would be promising a wiring the boot then refuses.
/// </para>
/// </summary>
public static class ConfigReferentialValidator
{
    /// <summary>Validate one project's refs against the catalog it will live in.</summary>
    public static void ValidateProject(ProjectEntity project, ConfigCatalog catalog)
    {
        var errors = new List<string>();

        Check(errors, project.Id, "agent", project.Agent, catalog.Agents.Select(a => a.Id));
        Check(errors, project.Id, "tracker", project.Tracker, catalog.Trackers.Select(t => t.Id));
        ValidateRepos(errors, project, catalog);

        if (errors.Count > 0)
        {
            var joined = string.Join("; ", errors);
            throw new ConfigurationException($"Referential integrity error(s): {joined}");
        }
    }

    private static void ValidateRepos(List<string> errors, ProjectEntity project, ConfigCatalog catalog)
    {
        var repoIds = catalog.Repos.Select(r => r.Id).ToList();
        var connectionIds = catalog.Connections.Select(c => c.Id).ToList();
        foreach (var repoRef in project.Repos)
        {
            // p0345b: a "connection/RepoName" (or connection/glob) ref resolves
            // against the CONNECTIONS catalog — valid iff the connection exists.
            // A plain ref keeps validating against the repos catalog. No ref form
            // is skipped: an unknown prefix is an error, never a silent pass.
            var slash = repoRef.IndexOf('/');
            if (slash > 0)
            {
                Check(errors, project.Id, "connection", repoRef[..slash], connectionIds,
                    $" (repo ref '{repoRef}')");
                continue;
            }
            Check(errors, project.Id, "repo", repoRef, repoIds);
        }
    }

    private static void Check(
        List<string> errors, string project, string kind, string reference,
        IEnumerable<string> catalogIds, string suffix = "")
    {
        var matches = catalogIds.Count(id => ConfigNames.AreSame(id, reference));
        if (matches == 1) return;
        errors.Add(matches == 0
            ? $"project '{project}' references unknown {kind} '{reference}'{suffix}"
            : $"project '{project}' references ambiguous {kind} '{reference}'{suffix} — "
              + $"{matches} catalog entries differ only in case, so none of them is loaded");
    }

    /// <summary>Validate every project in a catalog — used before a full save/export.</summary>
    public static void ValidateCatalog(ConfigCatalog catalog)
    {
        foreach (var project in catalog.Projects)
            ValidateProject(project, catalog);
    }
}
