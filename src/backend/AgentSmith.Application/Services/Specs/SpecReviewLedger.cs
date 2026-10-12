using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-042eh: keeps every spec's review report for the one pull-request body, the
/// same way <see cref="RunAccountLedger"/> keeps every spec's account for the one delivery
/// gate — and for the same reason. The per-spec key is cleared when the next spec is
/// selected; the pull request is built once, at the end.
/// <para>
/// A REPORT, not a finding list: a review that was skipped or failed carries its reason here
/// too, or the ledger would tell the pull request that a spec nobody read found nothing.
/// </para>
/// </summary>
public static class SpecReviewLedger
{
    /// <summary>Records the spec's report, replacing what a first review left.</summary>
    public static void Record(PipelineContext pipeline, PhaseReviewReport report)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(report);
        pipeline.Set(ContextKeys.SpecReviewFindings, report);
        pipeline.Set(ContextKeys.RunSpecReviews, Current(pipeline).With(SpecIdOf(pipeline), report));
    }

    /// <summary>The report of the spec now current, or <see cref="PhaseReviewReport.None"/>.</summary>
    public static PhaseReviewReport ForThisSpec(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<PhaseReviewReport>(
            ContextKeys.SpecReviewFindings, out var report) && report is not null
            ? report
            : PhaseReviewReport.None;
    }

    public static RunPhaseReviews Current(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<RunPhaseReviews>(ContextKeys.RunSpecReviews, out var reviews)
            && reviews is not null ? reviews : RunPhaseReviews.Empty;
    }

    private static string SpecIdOf(PipelineContext pipeline) =>
        pipeline.TryGet<PhaseDraft>(ContextKeys.CurrentSpec, out var draft) && draft is not null
            ? draft.PhaseId
            : "run";
}
