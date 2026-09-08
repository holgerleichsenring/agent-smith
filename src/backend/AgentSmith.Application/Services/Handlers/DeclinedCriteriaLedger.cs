using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-09-06-3d81: keeps every criterion the master DECLINED, per phase, for the run's
/// rendered outcome.
/// <para>
/// The verdict itself is published once per pass under ContextKeys.MasterVerification and
/// the next phase's pass overwrites it, so a run's earlier phases lose their answers before
/// CommitAndPR runs. RunAccountLedger keeps each phase's account for exactly that reason;
/// this keeps each phase's declined criteria. Only a not_applicable that carries its
/// evaluated reason is kept — the gate refuses a bare one, and this ledger carries answers,
/// not refusals.
/// </para>
/// </summary>
public static class DeclinedCriteriaLedger
{
    public static void Record(PipelineContext pipeline, MasterVerification verification)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(verification);
        var phaseId = PhaseIdOf(pipeline);
        var declined = (verification.AcceptanceDispositions ?? [])
            .Where(d => d.Status == AcceptanceStatus.NotApplicable && !string.IsNullOrWhiteSpace(d.Evidence))
            .Select(d => new DeclinedCriterion(d.Criterion.Trim(), d.Evidence.Trim(), phaseId))
            .ToList();
        pipeline.Set(ContextKeys.DeclinedCriteria, Current(pipeline).With(phaseId, declined));
    }

    public static DeclinedCriteria Current(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<DeclinedCriteria>(ContextKeys.DeclinedCriteria, out var declined) && declined is not null
            ? declined
            : DeclinedCriteria.Empty;
    }

    private static string? PhaseIdOf(PipelineContext pipeline) =>
        pipeline.TryGet<PhaseDraft>(ContextKeys.PhaseSpec, out var draft) && draft is not null
            ? draft.PhaseId
            : null;
}
