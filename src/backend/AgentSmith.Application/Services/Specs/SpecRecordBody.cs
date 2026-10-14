using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0420: the text of a spec record — the ratified spec, followed by the spec's own
/// account of what came of it. The spec says what was asked; without the account the
/// record cannot say what was delivered.
/// <para>
/// 2026-10-06-03c7e: the account and the review are an <c>outcome:</c> block of the spec's own
/// YAML, so the record is one document — the text of <c>specs/done/{stem}.yaml</c> and the body
/// the server is sent.
/// </para>
/// <para>
/// 2026-10-13-8762m: the build note's only home. A spec found already satisfied ran no master and
/// carries no key; a master that made no hand_forward call is recorded as <c>missing</c>.
/// </para>
/// </summary>
public static class SpecRecordBody
{
    public static string For(SpecDraft draft, PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(pipeline);
        var delivered = pipeline.TryGet<IReadOnlyList<SpecAccount>>(
            ContextKeys.SpecAccounts, out var accounts) && accounts is not null
                ? SpecAccountRenderer.ToMarkdown(accounts)
                : string.Empty;

        // 2026-09-17-042eh: what a fresh reviewer still finds in the diff, AFTER the account —
        // the account says what was delivered, the review what it costs to keep. A review that
        // was NOT taken says so here too: an absent answer is not a clean one.
        var review = SpecReviewSection.ForTheRecord(SpecReviewLedger.ForThisSpec(pipeline));
        var runId = pipeline.TryGet<string>(ContextKeys.RunId, out var id) ? id : null;
        return draft.Yaml.TrimEnd() + "\n" + SpecOutcomeBlock.Render(runId, delivered, review, HandForward(draft.SpecId, pipeline));
    }

    private static SpecBuildNoteRead? HandForward(string specId, PipelineContext pipeline)
    {
        var row = pipeline.TryGet<SpecSequenceProgress>(ContextKeys.SpecSequenceProgress, out var progress)
            ? progress?.Specs.FirstOrDefault(p => p.SpecId == specId) : null;
        if (row?.Note == Handlers.SelectSpecHandler.AlreadySatisfiedNote) return null;
        return pipeline.TryGet<IReadOnlyDictionary<string, SpecBuildNote>>(ContextKeys.SpecBuildNotes, out var notes)
            && notes is not null && notes.TryGetValue(specId, out var note)
                ? new SpecBuildNoteRead(note, false)
                : SpecBuildNoteRead.NoNote;
    }
}
