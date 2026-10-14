using AgentSmith.Application.Services.Loop;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Turns;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-10-13-8762i: one re-prompt of the design partner after a review round — reported as Revising, the
/// turn's thread continued, its cost tracked, the reply resolved like the turn's own. Null when the call failed
/// or the reply does not resolve to a proposal: the review then ends on the last valid one.
/// </summary>
public sealed class ProposalCutsReprompt(
    IAgenticLoopRunner loopRunner, IOutcomeProposalResolver resolver, ITurnActivityObserverAccessor turnActivity,
    ILogger<ProposalCutsReprompt> logger)
{
    public async Task<ProposalReply?> RunAsync(ProposalTurn turn, string nudge, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(turn);
        await turnActivity.ReportAsync(new TurnActivity(TurnActivityKind.Revising), ct);
        AgenticLoopResult result;
        try
        {
            result = await loopRunner.RunAsync(
                turn.Request with { UserPrompt = nudge, PriorMessages = turn.Conversation.Thread() }, ct);
            turn.Conversation.Continued(nudge, result.Response);
            turn.CostTracker.Track(result.Response);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "The design partner could not be re-prompted with the review's cuts");
            return null;
        }
        IReadOnlyList<string> scope = [.. DerivationLookFactory.ProposalRepositories(turn.Pipeline)?.Keys ?? []];
        if (resolver.Resolve(result.Response.Text ?? string.Empty, scope) is OutcomeResolved resolved)
            return new ProposalReply(result, resolved.Proposal);
        logger.LogWarning("The design partner's re-proposal after the review did not resolve to a proposal");
        return null;
    }
}
