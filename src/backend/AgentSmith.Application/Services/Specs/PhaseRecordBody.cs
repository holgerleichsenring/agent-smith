using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0420: the text of a phase record — the ratified spec, followed by the phase's own
/// account of what came of it. The spec says what was asked; without the account the
/// record cannot say what was delivered.
/// <para>
/// 2026-10-06-03c7e: the account and the review are an <c>outcome:</c> block of the spec's own
/// YAML, so the record is one document — the text of <c>specs/done/{stem}.yaml</c> and the body
/// the server is sent.
/// </para>
/// </summary>
public static class PhaseRecordBody
{
    public static string For(PhaseDraft draft, PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(pipeline);
        var delivered = pipeline.TryGet<IReadOnlyList<SpecAccount>>(
            ContextKeys.PhaseAccounts, out var accounts) && accounts is not null
                ? SpecAccountRenderer.ToMarkdown(accounts)
                : string.Empty;

        // 2026-09-17-042eh: what a fresh reviewer still finds in the diff, AFTER the account —
        // the account says what was delivered, the review what it costs to keep. A review that
        // was NOT taken says so here too: an absent answer is not a clean one.
        var review = PhaseReviewSection.ForTheRecord(PhaseReviewLedger.ForThisPhase(pipeline));
        var runId = pipeline.TryGet<string>(ContextKeys.RunId, out var id) ? id : null;
        return draft.Yaml.TrimEnd() + "\n" + SpecOutcomeBlock.Render(runId, delivered, review);
    }
}
