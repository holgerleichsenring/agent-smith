using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Specs;
using AgentSmith.Application.Services.Sandbox;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-01-283dc: tells the master what a <c>reference:</c> address IS — material the operator
/// uploaded to this conversation, read with the tools a repository is read with. Built over the
/// address list like <see cref="TemplatePromptSection"/>, and empty when there is none.
/// 2026-10-02-075da: MATERIAL, NOT A WEBSITE. An upload keeps every authored file, so it may be a
/// site, an application's source or documents, and finding out which is the model's first job.
/// </summary>
internal static class ReferencePromptSection
{
    /// <param name="sandboxes">2026-10-02-075dd: the turn's map, where each upload's sandbox carries its note.</param>
    internal static string Build(IReadOnlyList<string> addresses, IReadOnlyDictionary<string, ISandbox>? sandboxes = null)
    {
        var references = addresses?
            .Where(a => a.StartsWith(ReferenceScopeName.Prefix, StringComparison.Ordinal))
            .ToList();
        if (references is null || references.Count == 0) return string.Empty;
        var bullets = string.Join("\n", references.Select(n => $"- `{n}`" + ReferenceNoteText.Under(NoteOf(sandboxes, n))));
        return "\n\n## Material the operator uploaded\n"
            + "These addresses are what the operator uploaded to this conversation, every file at "
            + "its own path — a website, an application's source, documents; find out which before "
            + "you rely on it. Read them with read_file, grep and directory_tree; a write comes back "
            + "refused. A browser upload carries no file mode, so a script is not executable.\n"
            + bullets + "\n\n"
            + "A stylesheet states exact values — colours, sizes, spacing, fonts. When the operator "
            + "asks for something to look like it, read the value there rather than estimating "
            + "it from a screenshot.\n";
    }

    private static string? NoteOf(IReadOnlyDictionary<string, ISandbox>? sandboxes, string address) =>
        sandboxes?.GetValueOrDefault(address) is ReferenceSetSandbox set ? set.Note : null;

    /// <summary>
    /// 2026-10-01-283df: the uploaded sets a RUN carries — each by name, id and directory inside the
    /// carrying repository (prefixed by its name when the master addresses several), outside the
    /// commit and outside a whole-repository search. Empty when the run carries none.
    /// </summary>
    internal static string Carried(PipelineContext pipeline, bool prefixed)
    {
        if (!pipeline.TryGet<IReadOnlyList<CarriedReferenceSet>>(ContextKeys.ReferenceSets, out var sets)
            || sets is null || sets.Count == 0) return string.Empty;
        var bullets = string.Join("\n", sets.Select(s =>
            $"- {s.Name} (set {s.SetId}, {s.Files} files): `{(prefixed ? s.Repo + "/" : string.Empty)}{s.Path}/` — render it as `{s.Address}`"
            + ReferenceNoteText.Carried(s.Note)));
        return "\n\n## Material the approval cites\n"
            + "The person who approved this work uploaded this material as the reference to build "
            + "against — a website, an application's source, documents. Each is in its own directory of the repository, NOT part of the commit — "
            + "never edit or move them. A whole-repository grep or directory_tree skips them; start "
            + "the search inside the directory to read them.\n"
            + bullets + "\n\n"
            + "A stylesheet states exact values. When render_reference is on your surface, the name "
            + "after 'render it as' renders a website among them.\n";
    }
}
