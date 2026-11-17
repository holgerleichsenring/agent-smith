using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-11-12-cd4ci: what a design turn's proposal is checked for when no review pass runs — read, no model call: a
/// spec's or an epic's cited files, a ticket list's ticket:&lt;id&gt; requirements and (2026-11-17-5701a) its specs'
/// cited files. The run reviews every spec before it is built either way.
/// </summary>
public sealed class ProposalMechanicalChecks(
    ProposalEvidenceReview evidence, TicketPlanReview plans, ILogger<ProposalMechanicalChecks> logger)
{
    public async Task<OutcomeProposal> CheckAsync(PipelineContext pipeline, OutcomeProposal proposal, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(proposal);
        IReadOnlyList<ProposalFinding> kept = proposal is TicketGraphOutcome graph
            ? (await plans.CheckAsync(pipeline, graph, ct)).Findings
            : [];
        try
        {
            return proposal with { Findings = [.. kept, .. await evidence.FindingsAsync(pipeline, proposal, ct)] };
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The evidence of this design turn's proposal could not be checked");
            return kept.Count == 0 ? proposal : proposal with { Findings = kept };
        }
    }
}
