using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-01-283dc: tells the master what a <c>reference:</c> address IS — a website the operator
/// uploaded to this conversation, read-only, read with the tools a repository is read with. Built
/// over the address list like <see cref="TemplatePromptSection"/>, and empty when there is none.
/// </summary>
internal static class ReferencePromptSection
{
    internal static string Build(IReadOnlyList<string> addresses)
    {
        var references = addresses?
            .Where(a => a.StartsWith(ReferenceScopeName.Prefix, StringComparison.Ordinal))
            .ToList();
        if (references is null || references.Count == 0) return string.Empty;
        var bullets = string.Join("\n", references.Select(n => $"- `{n}`"));
        return "\n\n## Websites the operator uploaded\n"
            + "These addresses are a website the operator uploaded to this conversation — its "
            + "HTML, CSS, scripts and assets at their own paths. They are READ-ONLY: read them "
            + "with read_file, grep and directory_tree; a write comes back refused.\n"
            + bullets + "\n\n"
            + "Its CSS states exact values — colours, sizes, spacing, fonts. When the operator "
            + "asks for something to look like it, read the value there rather than estimating "
            + "it from a screenshot.\n";
    }

    /// <summary>
    /// 2026-10-01-283df: the websites a RUN carries — each by name, id and directory inside the
    /// carrying repository (prefixed by its name when the master addresses several), outside the
    /// commit and outside a whole-repository search. Empty when the run carries none.
    /// </summary>
    internal static string Carried(PipelineContext pipeline, bool prefixed)
    {
        if (!pipeline.TryGet<IReadOnlyList<CarriedReferenceSet>>(ContextKeys.ReferenceSets, out var sets)
            || sets is null || sets.Count == 0) return string.Empty;
        var bullets = string.Join("\n", sets.Select(s =>
            $"- {s.Name} (set {s.SetId}, {s.Files} files): `{(prefixed ? s.Repo + "/" : string.Empty)}{s.Path}/` — render it as `{s.Address}`"));
        return "\n\n## Websites the approval cites\n"
            + "The person who approved this work uploaded these websites as the reference to build "
            + "against. Each is in its own directory of the repository, NOT part of the commit — "
            + "never edit or move them. A whole-repository grep or directory_tree skips them; start "
            + "the search inside the directory to read them.\n"
            + bullets + "\n\n"
            + "Their CSS states exact values. When render_reference is on your surface, the name "
            + "after 'render it as' renders that website.\n";
    }
}
