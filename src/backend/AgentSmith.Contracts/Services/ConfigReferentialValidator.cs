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
        foreach (var repoRef in project.Repos)
        {
            // A repo ref may be a plain catalog name or a "connection/glob" form;
            // only plain catalog names are checked against the repo catalog here.
            if (repoRef.Contains('/')) continue;
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
