using AgentSmith.Application.Services.Specs;

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
    internal static string Build(IReadOnlyList<string> addresses)
    {
        var templateNames = addresses?
            .Where(a => a.StartsWith(TemplateScopeName.Prefix, StringComparison.Ordinal))
            .ToList();
        if (templateNames is null || templateNames.Count == 0) return string.Empty;
        var bullets = string.Join("\n", templateNames.Select(n => $"- `{n}`"));
        return "\n\n## Templates this work is built after\n"
            + "These addresses are READ-ONLY checkouts of another project, at the revision "
            + "this project declared. Read them with the same tools you read a repository "
            + "with; write_file, edit and run_command into them come back refused.\n"
            + bullets + "\n\n"
            + "A template answers HOW something here is built — the naming, the layering, "
            + "the shape a new file takes, and where one slice of work ends. It never "
            + "answers WHAT to build: that is the phase spec, or the feature under "
            + "discussion, and a template that disagrees with it is out of date, not "
            + "authoritative. Follow the source order your instructions already state.\n";
    }
}
