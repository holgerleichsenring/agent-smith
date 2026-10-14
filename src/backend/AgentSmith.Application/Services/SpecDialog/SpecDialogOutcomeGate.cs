using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Turns;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// p0315b/p0315e: resolves a design turn's typed terminal outcome (answer / bug / spec / epic) and publishes it for
/// CollectSpecDialogReply, after refusing a proposal the turn may not make yet. An invalid outcome re-prompts the
/// master ONCE with the exact error; a second failure replaces the reply with an honest notice, so the raw invalid
/// output never reaches the thread.
/// </summary>
public sealed class SpecDialogOutcomeGate(
    IAgenticLoopRunner loopRunner,
    ISpecDialogPromptFactory promptFactory,
    IOutcomeProposalResolver outcomeResolver,
    SpecDialogProposalRefusal proposalRefusal, // 2026-09-17-042ec: no proposal before a discussion
    ITurnActivityObserverAccessor turnActivity, // 2026-09-17-042ee: the turn's own steps
    ILogger<SpecDialogOutcomeGate> logger)
{
    public async Task<AgenticLoopResult> GateAsync(
        PipelineContext pipeline, AgenticLoopRequest request, string userPrompt,
        AgenticLoopResult loopResult, MasterConversation conversation, PipelineCostTracker costTracker, CancellationToken ct)
    {
        loopResult = await proposalRefusal.RefuseEarlyProposalAsync(pipeline, request, userPrompt, loopResult, conversation, costTracker, ct);
        // 2026-10-12-3a36e: the turn's repositories, keyed by their configured names in the dialog.
        IReadOnlyList<string> scope = [.. Specs.DerivationLookFactory.ProposalRepositories(pipeline)?.Keys ?? []];
        var resolution = outcomeResolver.Resolve(loopResult.Response.Text ?? string.Empty, scope);
        if (resolution is OutcomeResolved first)
            return MasterOutcomes.PublishOutcome(pipeline, first.Proposal, loopResult);
        var invalid = (OutcomeInvalid)resolution;
        logger.LogWarning("Design-partner terminal outcome failed validation — re-prompting once: {Error}", invalid.Error);
        await turnActivity.ReportAsync(new TurnActivity(TurnActivityKind.Revising), ct);
        return await RetryAsync(pipeline, request, userPrompt, loopResult, conversation, costTracker, invalid, scope, ct);
    }

    // 2026-09-24-3907: WITH the thread, like every sibling re-prompt. Without it the runner assembles [system] + nudge
    // and nothing else, so a model told "do not repeat the invalid output" holds no copy of it.
    private async Task<AgenticLoopResult> RetryAsync(
        PipelineContext pipeline, AgenticLoopRequest request, string userPrompt, AgenticLoopResult loopResult,
        MasterConversation conversation, PipelineCostTracker costTracker, OutcomeInvalid invalid,
        IReadOnlyList<string> scope, CancellationToken ct)
    {
        AgenticLoopResult retry;
        try
        {
            var nudge = promptFactory.BuildOutcomeFixNudge(userPrompt, invalid.Error);
            retry = await loopRunner.RunAsync(request with { UserPrompt = nudge, PriorMessages = conversation.Thread() }, ct);
            conversation.Continued(nudge, retry.Response);
            costTracker.Track(retry.Response);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Outcome-fix re-prompt failed");
            return MasterOutcomes.FailOutcome(pipeline, loopResult, invalid.Error);
        }
        var resolution = outcomeResolver.Resolve(retry.Response.Text ?? string.Empty, scope);
        if (resolution is OutcomeResolved second) return MasterOutcomes.PublishOutcome(pipeline, second.Proposal, retry);
        var stillInvalid = (OutcomeInvalid)resolution;
        logger.LogWarning("Design-partner terminal outcome still invalid after re-prompt: {Error}", stillInvalid.Error);
        return MasterOutcomes.FailOutcome(pipeline, retry, stillInvalid.Error);
    }
}
