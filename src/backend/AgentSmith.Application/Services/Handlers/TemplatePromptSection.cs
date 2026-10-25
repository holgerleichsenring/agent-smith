using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-13-6f35: tells the master what the template addresses in its repository list
/// ARE. Without it they read as one more checkout to change, which is the one thing they
/// are not.
/// <para>
/// The four-way source order (principles &gt; template for what is NEW &gt; existing code
/// for an EXTENSION &gt; prototype and design give only the WHAT) is the catalog master's
/// own <c>source-precedence</c> section, shipped by 2026-09-13-ab17. This section points at
/// it rather than restating it — a second copy is the copy that goes stale.
/// </para>
/// </summary>
internal static class TemplatePromptSection
{
    /// <param name="addresses">
    /// 2026-09-13-ed5a: every address the run can reach, template or repository — the
    /// <c>template:</c> prefix is what tells them apart, and it is the naming contract
    /// already. Taking the whole list is what lets ONE section serve both surfaces: the
    /// coding master appends its templates to the address list, while a spec-dialog turn
    /// seeds them into the sandbox map the list is read from.
    /// </param>
    /// <param name="declared">2026-10-25-54bea: the declaration and landed revision behind each
    /// address the coding master opened; null on the spec-dialog path, which keeps the address-only form.</param>
    internal static string Build(
        IReadOnlyList<string> addresses,
        IReadOnlyDictionary<string, (ProjectTemplate Declared, string? Sha)>? declared = null)
    {
        var templateNames = addresses?
            .Where(a => a.StartsWith(TemplateScopeName.Prefix, StringComparison.Ordinal))
            .ToList();
        if (templateNames is null || templateNames.Count == 0) return string.Empty;
        var bullets = string.Join("\n", templateNames.Select(n =>
            declared is not null && declared.TryGetValue(n, out var d) ? $"- `{n}` — {Detail(d.Declared, d.Sha)}" : $"- `{n}`"));
        return "\n\n## Templates this work is built after\n"
            + "These addresses are READ-ONLY checkouts of another project, at the revision "
            + "this project declared. Read them with the same tools you read a repository "
            + "with; write_file, edit and run_command into them come back refused.\n"
            + bullets + "\n\n"
            + "A template answers HOW something here is built — the naming, the layering, "
            + "the shape a new file takes, and where one slice of work ends. It never "
            + "answers WHAT to build: that is the spec, or the feature under "
            + "discussion, and a template that disagrees with it is out of date, not "
            + "authoritative. Follow the source order your instructions already state.\n";
    }

    /// <summary>2026-10-25-54bea: which context of which repository an address is, at which
    /// revision, and where in it that context's own files live.</summary>
    internal static string Detail(ProjectTemplate declared, string? sha)
    {
        var project = string.IsNullOrWhiteSpace(declared.Repo.Project) ? "" : $" (project `{declared.Repo.Project}`)";
        var revision = sha ?? declared.Revision ?? "its default revision";
        return $"the `{declared.TemplateContext}` context of `{declared.Repo.Name}`{project} at `{revision}`, "
            + $"built after for this project's `{declared.Context}` context; its files are under "
            + $"`{ProjectMetaPaths.Contexts}/{declared.TemplateContext}/`";
    }
}
