using AgentSmith.Application.Services.Specs.Review;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-11-12-cd4ci: what a design turn's proposal is checked for — read, no model call: a spec's or an epic's cited
/// files, a ticket list's ticket:&lt;id&gt; requirements and (2026-11-17-5701a) its specs' cited files. 2026-11-18-9c1ca:
/// the only checks a design turn's draft gets from outside its master; the run reviews every spec before it is built.
/// </summary>
public sealed class ProposalMechanicalChecks(
    ProposalEvidenceReview evidence, TicketExistenceFindings existence, ILogger<ProposalMechanicalChecks> logger)
{
    /// <summary>The spec id a finding about a ticket list as a whole carries.</summary>
    public const string ListKey = "the list";

    /// <summary>The problem of the one finding a list in a ticket's own conversation gets.</summary>
    public const string BoundProblem = "bound conversation";

    public const string BoundWhy =
        "a list of tickets is filed from Work it out without a ticket; this conversation continues its own ticket";

    public async Task<OutcomeProposal> CheckAsync(PipelineContext pipeline, OutcomeProposal proposal, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        ArgumentNullException.ThrowIfNull(proposal);
        if (proposal is not TicketGraphOutcome && ProposalSpecSet.Drafts(proposal).Count == 0) return proposal;
        IReadOnlyList<ProposalFinding> kept = proposal is TicketGraphOutcome graph
            ? await ListFindingsAsync(pipeline, graph, ct)
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

    private async Task<IReadOnlyList<ProposalFinding>> ListFindingsAsync(
        PipelineContext pipeline, TicketGraphOutcome graph, CancellationToken ct) =>
        pipeline.Has(ContextKeys.SpecDialogTicket)
            ? [new ProposalFinding(ListKey, BoundProblem, BoundWhy)]
            : await existence.FindingsAsync(pipeline, graph, ct);
}
