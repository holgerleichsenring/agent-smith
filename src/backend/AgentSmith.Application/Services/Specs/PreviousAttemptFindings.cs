using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-18-550ab: the findings the ticket's previous attempt stored for the spec being worked, carried to this
/// attempt's verification of it so the verifier keeps its word across a Retry as d6d2c made it keep it across a repair.
/// Carried only when the previous attempt FAILED (its work stays on the ticket branch; a shortfall reverted it) and this
/// run works that branch (not stacked, not cut fresh); a row whose criterion the spec no longer states is dropped. The
/// spec's start heads become the repair base, so the files this attempt changed are named as a repair's are.
/// </summary>
public sealed class PreviousAttemptFindings(IRunSpecAcceptanceReader acceptance, ILogger<PreviousAttemptFindings> logger)
{
    public async Task RecordAsync(PipelineContext pipeline, string specId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<PreviousAttempt>(ContextKeys.PreviousAttempt, out var attempt) || attempt is null
            || attempt.Status != RunStatuses.Failed || !WorksTheTicketBranch(pipeline))
            return;
        var stored = await acceptance.ReadAsync(attempt.RunId, specId, cancellationToken);
        var criteria = AcceptanceCriteria.For(pipeline).ToHashSet(StringComparer.Ordinal);
        var findings = (stored?.Criteria ?? [])
            .Where(c => c.Status == AcceptanceCriterionStatuses.Unmet && criteria.Contains(c.Text))
            .Select(c => string.IsNullOrWhiteSpace(c.Reason) ? c.Text : $"{c.Text} ({c.Reason})")
            .ToList();
        if (findings.Count == 0) return;
        pipeline.Set(ContextKeys.PreviousAttemptFindings, findings);
        if (pipeline.TryGet<IReadOnlyDictionary<string, string>>(ContextKeys.SpecStartHeads, out var heads) && heads is not null)
            pipeline.Set<IReadOnlyDictionary<string, string>>(ContextKeys.RepairBaseHeads, heads);
        logger.LogInformation("Spec {SpecId}: {Count} finding(s) of attempt {RunId} carried to this attempt's verification",
            specId, findings.Count, attempt.RunId);
    }

    private static bool WorksTheTicketBranch(PipelineContext pipeline) =>
        RunBranchResolver.Resolve(pipeline) is { ComposedFromTicket: true, Stacked: false, CutFresh: false };
}
