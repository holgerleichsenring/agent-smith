using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0421: keeps every phase's account for the run's own gate.
/// <para>
/// The delivery question is asked once, at the end, about the whole run — so the answer
/// each phase gave has to survive the next phase. Keyed by phase id, so a re-run of the
/// same phase replaces its account instead of counting twice.
/// </para>
/// </summary>
public static class RunAccountLedger
{
    public static void Record(PipelineContext pipeline, IReadOnlyList<SpecAccount> accounts)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (accounts is not { Count: > 0 }) return;
        var phaseId = pipeline.TryGet<PhaseDraft>(ContextKeys.PhaseSpec, out var draft) && draft is not null
            ? draft.PhaseId
            : "run";
        pipeline.Set(ContextKeys.RunAccounts, Current(pipeline).With(phaseId, accounts));
    }

    public static RunAccounts Current(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<RunAccounts>(ContextKeys.RunAccounts, out var accounts) && accounts is not null
            ? accounts
            : RunAccounts.Empty;
    }
}
