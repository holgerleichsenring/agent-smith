using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-17-a196c: whether the open loop objects to the master's met claims before it ends — only when the
/// acceptance gate is otherwise satisfied, only on a code pipeline, and only once per master step: a
/// citation that cannot resolve would otherwise drive passes until the budget, and after the one objection
/// the keystone judges what follows. Returns the unresolved lines, empty when there is nothing to object to.
/// </summary>
public sealed class AcceptanceClaimObjection(AcceptanceClaimEvidence evidence, AcceptanceClaimCheck check)
{
    internal async Task<IReadOnlyList<string>> LinesAsync(
        ReengageSession s, ReengageState state, IReadOnlyList<string> criteria, bool objectedAlready,
        CancellationToken ct)
    {
        if (objectedAlready || criteria.Count == 0 || s.CostTracker.IsBudgetExhausted) return [];
        if (string.IsNullOrEmpty(s.PipelineName) || !PipelinePresets.ExpectsCodeChanges(s.PipelineName)) return [];
        if (!MasterAcceptanceGate.ObjectivelySatisfied(state.Verification, criteria, state.Changes.Count > 0)) return [];
        var resolver = await evidence.GatherAsync(s.Context.Pipeline, ct);
        return resolver is null ? [] : check.Unresolved(state.Verification, criteria, resolver);
    }
}
