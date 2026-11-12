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
    /// <summary>2026-11-11-205fi: the address list only, in the system prompt — it changes when an upload does.</summary>
    internal static string Build(IReadOnlyList<string> addresses)
    {
        var references = addresses?
            .Where(a => a.StartsWith(ReferenceScopeName.Prefix, StringComparison.Ordinal))
            .ToList();
        if (references is null || references.Count == 0) return string.Empty;
        var bullets = string.Join("\n", references.Select(n => $"- `{n}`"));
        return "\n\n## Material the operator uploaded\n"
            + "These addresses are what the operator uploaded to this conversation, every file at "
            + "its own path — a website, an application's source, documents; find out which before "
            + "you rely on it. Read them with read_file, grep_in_tree and directory_tree.\n" // 2026-11-11-205fd
            + bullets + "\n\n"
            + "A stylesheet states exact values — colours, sizes, spacing, fonts. When the operator "
            + "asks for something to look like it, read the value there rather than estimating "
            + "it from a screenshot.\n";
    }

    /// <summary>
    /// 2026-11-11-205fi: each upload's coverage, in the design turn's user prompt — it changes when a turn opens files,
    /// so in the system prompt it broke its cache on every such turn. Empty when the turn carries no upload.
    /// 2026-11-12-cd4ck: the per-upload note is gone; the conversation's notes render on their own.
    /// </summary>
    internal static string ThisTurn(IReadOnlyDictionary<string, ISandbox>? sandboxes)
    {
        var sets = (sandboxes ?? new Dictionary<string, ISandbox>())
            .Where(kv => kv.Key.StartsWith(ReferenceScopeName.Prefix, StringComparison.Ordinal))
            .Select(kv => (Address: kv.Key, Set: kv.Value as ReferenceSetSandbox))
            .OrderBy(s => s.Address, StringComparer.Ordinal)
            .ToList();
        if (sets.Count == 0) return string.Empty;
        return "## Uploads at this turn\n" + string.Join("\n", sets.Select(s => $"- `{s.Address}`"
            + ReferenceCoverageText.Under(s.Set?.Coverage)));
    }

    /// <summary>
    /// 2026-10-01-283df: the uploaded sets a RUN carries — each by name, id and directory inside the
    /// carrying repository (prefixed by its name when the master addresses several), outside the
    /// commit and outside a whole-repository search. Empty when the run carries none.
    /// </summary>
    /// <param name="attached">2026-10-14-dbcel: false for the derivation, a text call that attaches no image.</param>
    internal static string Carried(PipelineContext pipeline, bool prefixed, bool attached = true) =>
        CarriedSets(pipeline, prefixed) + CarriedImages(pipeline, attached);

    // 2026-10-08-e8b9k: the images the approval cites — files beside the sets; the attachment note
    // says which of them ride the message as pictures.
    private static string CarriedImages(PipelineContext pipeline, bool attached)
    {
        if (!pipeline.TryGet<IReadOnlyList<CarriedReferenceImage>>(ContextKeys.ReferenceImages, out var images)
            || images is null || images.Count == 0) return string.Empty;
        return "\n\n## Images the approval cites\n"
            + "The person who approved this work attached these images. They are files outside the commit — never "
            + "edit or move them; copy one into the repository when the work needs it as an asset.\n"
            + string.Join("\n", images.Select(i => i.Path is null
                ? $"- image {i.SetId}: could not be read in this run"
                : $"- `{i.Path}`{(attached && i.Shown ? " — attached to this message" : string.Empty)}")) + "\n";
    }

    private static string CarriedSets(PipelineContext pipeline, bool prefixed)
    {
        if (!pipeline.TryGet<IReadOnlyList<CarriedReferenceSet>>(ContextKeys.ReferenceSets, out var sets)
            || sets is null || sets.Count == 0) return string.Empty;
        // 2026-11-12-cd4ck: the notes are the conversation's, so they follow the first set of each conversation only.
        var bullets = string.Join("\n", sets.Select((s, i) =>
            $"- {s.Name} (set {s.SetId}, {s.Files} files): `{(prefixed ? s.Repo + "/" : string.Empty)}{s.Path}/` — render it as `{s.Address}`"
            + (sets.Take(i).Any(earlier => earlier.Session == s.Session) ? string.Empty : ReferenceNoteText.Carried(s.Note))));
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
