using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0420: the text of a phase record — the ratified spec, followed by the phase's own
/// account of what came of it. The spec says what was asked; without the account the
/// record cannot say what was delivered.
/// </summary>
public static class PhaseRecordBody
{
    public static string For(PhaseDraft draft, PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(pipeline);
        var account = pipeline.TryGet<IReadOnlyList<SpecAccount>>(
            ContextKeys.PhaseAccounts, out var accounts) && accounts is not null
                ? SpecAccountRenderer.ToMarkdown(accounts)
                : string.Empty;

        // 2026-09-17-042eh: and what a fresh reviewer still finds in the diff, AFTER the
        // account — the account says what was delivered, the review what it costs to keep.
        // A review that was NOT taken says so here too: an absent answer is not a clean one.
        var review = PhaseReviewSection.ForTheRecord(PhaseReviewLedger.ForThisPhase(pipeline));

        var spec = draft.Yaml.TrimEnd() + "\n";
        var body = (account + (account.Length > 0 && review.Length > 0 ? "\n" : string.Empty) + review)
            .TrimEnd();
        if (body.Length == 0) return spec;
        return spec + "\n# " + body.Replace("\n", "\n# ") + "\n";
    }
}
