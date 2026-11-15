using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog.Writes;

/// <summary>
/// 2026-11-15-06fc: a draft as the files the design master reads and changes — a view of the one typed draft the
/// store holds, never a second copy of it. An epic's parent comes first, then the specs, the series contract, the
/// tickets, the bug and the comment. A spec's file is its stored text.
/// </summary>
public static class DraftFiles
{
    public static IReadOnlyList<DraftFile> Of(ProposalDraft draft)
    {
        ArgumentNullException.ThrowIfNull(draft);
        var files = new List<DraftFile>();
        foreach (var spec in draft.Specs.OrderBy(s => s.Parent ? 0 : 1))
            files.Add(new(DraftPath.OfSpec(spec.Draft.SpecId, spec.Parent), spec.Draft.Yaml.TrimEnd() + "\n"));
        if (draft.Contract is { } contract) files.Add(new(DraftPath.Contract, DraftContractText.Render(contract)));
        files.AddRange(draft.Tickets.Select(t => new DraftFile(DraftPath.OfTicket(t.Key), DraftTicketText.Render(t))));
        if (draft.Bug is { } bug) files.Add(new(DraftPath.Bug, DraftBugText.Render(bug)));
        if (draft.Comment is { } comment) files.Add(new(DraftPath.Comment, comment.TrimEnd() + "\n"));
        return files;
    }

    /// <summary>The file at a bare path, or null.</summary>
    public static DraftFile? Find(ProposalDraft draft, string bare) =>
        Of(draft).FirstOrDefault(f => string.Equals(f.Path, bare, StringComparison.Ordinal));

    /// <summary>What the draft holds, as its paths.</summary>
    public static string Listed(ProposalDraft draft)
    {
        var paths = Of(draft).Select(f => DraftPath.Shown(f.Path)).ToList();
        return paths.Count == 0 ? "no file yet" : string.Join(", ", paths);
    }

    /// <summary>The turn's draft files as the read tools see them, or null on a pipeline with no reply slot.</summary>
    public static Func<IReadOnlyList<DraftFile>>? Reader(PipelineContext pipeline) =>
        pipeline.TryGet<SpecDialogReplySlot>(ContextKeys.SpecDialogReplySlot, out var slot) && slot is not null
            ? () => Of(slot.Proposal.Current)
            : null;
}
