using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-09-13-5fa0: the four things a template binding may not be. Stated once and read
/// from BOTH sides — the loader, which is the only path that can author one today, and the
/// studio write, which mirrors it so a broken wiring cannot be persisted either way.
/// <para>
/// A CONTEXT NAME IS NOT AMONG THEM, and that is the likeliest typo. Neither ProjectEntity
/// nor ConfigCatalog carries contexts — they are discovered inside a run — so both sides of
/// the binding are strings nothing here can check. They are refused where the template is
/// fetched and its contexts are discovered.
/// </para>
/// </summary>
public static class ProjectTemplateRules
{
    /// <summary>One message per broken rule, empty when the list is sound.</summary>
    public static IReadOnlyList<string> Check(
        string project,
        IReadOnlyList<TemplateReference> templates,
        IReadOnlyDictionary<string, IReadOnlyList<string>> repoRefsByProject)
    {
        ArgumentNullException.ThrowIfNull(templates);
        ArgumentNullException.ThrowIfNull(repoRefsByProject);
        var errors = new List<string>();

        repoRefsByProject.TryGetValue(project, out var ownRepoRefs);
        foreach (var template in templates)
        {
            // 2026-09-16-4df5: the LOCAL repo, when the binding names one. A name this project
            // does not carry would address a scope no run can open — and it is the likeliest
            // typo now that the field exists, because it is the one repo ref an operator can
            // write without the target project in front of them.
            if (!string.IsNullOrWhiteSpace(template.ContextRepo) && ownRepoRefs is not null
                && !ownRepoRefs.Any(r => ConfigNames.AreSame(r, template.ContextRepo)))
            {
                errors.Add($"project '{project}': template for context '{template.Context}' "
                    + $"says that context lives in repo '{template.ContextRepo}', which this "
                    + "project does not carry");
                continue;
            }
            if (!repoRefsByProject.TryGetValue(template.Project, out var repoRefs))
            {
                errors.Add($"project '{project}': template for context '{template.Context}' "
                    + $"names unknown project '{template.Project}'");
                continue;
            }
            if (template.Repo.Contains('*', StringComparison.Ordinal))
            {
                errors.Add($"project '{project}': template for context '{template.Context}' "
                    + $"names glob repo ref '{template.Repo}' — a wildcard names a set nobody "
                    + "enumerated, and a template is one repository");
                continue;
            }
            if (!repoRefs.Any(reference => ConfigNames.AreSame(reference, template.Repo)))
                errors.Add($"project '{project}': template for context '{template.Context}' "
                    + $"names repo ref '{template.Repo}', which project '{template.Project}' "
                    + "does not carry");
        }

        return errors;
    }

    /// <summary>
    /// Every cycle the whole catalog carries, one message each. A cycle is only visible
    /// across projects, so it is checked over the graph rather than per entry — the store
    /// hands a single-project save the whole catalog, so this is reachable from both sides.
    /// </summary>
    public static IReadOnlyList<string> CheckCycles(
        IReadOnlyDictionary<string, IReadOnlyList<string>> templateTargetsByProject)
    {
        ArgumentNullException.ThrowIfNull(templateTargetsByProject);
        var errors = new List<string>();
        foreach (var project in templateTargetsByProject.Keys.OrderBy(k => k, StringComparer.Ordinal))
        {
            var path = new List<string>();
            if (Walks(project, project, templateTargetsByProject, path, new HashSet<string>(StringComparer.Ordinal)))
                errors.Add($"project '{project}': template cycle {string.Join(" -> ", path)} -> {project}");
        }
        return errors;
    }

    private static bool Walks(
        string start, string current,
        IReadOnlyDictionary<string, IReadOnlyList<string>> targets,
        List<string> path, HashSet<string> seen)
    {
        if (!seen.Add(current)) return false;
        path.Add(current);
        if (!targets.TryGetValue(current, out var next)) return false;
        foreach (var target in next)
        {
            if (ConfigNames.AreSame(target, start)) return true;
            if (Walks(start, target, targets, path, seen)) return true;
        }
        path.RemoveAt(path.Count - 1);
        return false;
    }
}
