using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-09-15-9b3e: <see cref="ProjectTemplateRules"/> run over ONE project entity against
/// the stored catalog, RETURNING the messages rather than throwing them — so the studio's
/// draft check can report a broken binding on the field before Save, instead of the operator
/// meeting it as a 400 afterwards.
/// <para>
/// A caller of the rules, never a second copy of them: the referential validator calls this
/// too, so there is one statement of what a template binding may not be.
/// </para>
/// </summary>
public static class ProjectTemplateDraftCheck
{
    /// <summary>
    /// The messages, or none.
    /// <para>
    /// A binding that is merely UNFINISHED yields nothing. The form re-posts the draft on
    /// every keystroke and Add appends an empty binding, so judging those would report
    /// "names unknown project ''" on a row whose own badge already says it is unfinished.
    /// </para>
    /// </summary>
    public static IReadOnlyList<string> MessagesFor(ProjectEntity project, ConfigCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(catalog);
        var errors = new List<string>();
        if (project.Templates is not { Count: > 0 } declared) return errors;
        var templates = declared.Where(IsAnswered).ToList();
        if (templates.Count == 0) return errors;

        // The draft is injected over the catalog: a NEW project is not stored yet, and its
        // own repos and targets are what the rules and the cycle walk have to see.
        var repoRefsByProject = catalog.Projects
            .ToDictionary(p => p.Id, p => p.Repos, ConfigNames.Comparer);
        repoRefsByProject[project.Id] = project.Repos;
        errors.AddRange(ProjectTemplateRules.Check(project.Id, templates, repoRefsByProject));

        var targets = catalog.Projects
            .ToDictionary(p => p.Id, p => Targets(p.Templates), ConfigNames.Comparer);
        targets[project.Id] = Targets(templates);
        errors.AddRange(ProjectTemplateRules.CheckCycles(targets));
        return errors;
    }

    /// <summary>Whether this binding says enough to be judged at all.</summary>
    private static bool IsAnswered(TemplateReference t) =>
        !string.IsNullOrWhiteSpace(t.Context) && !string.IsNullOrWhiteSpace(t.Project)
        && !string.IsNullOrWhiteSpace(t.Repo) && !string.IsNullOrWhiteSpace(t.TemplateContext);

    private static IReadOnlyList<string> Targets(IReadOnlyList<TemplateReference>? templates) =>
        templates is null ? [] : [.. templates.Select(t => t.Project).Distinct(ConfigNames.Comparer)];
}
