using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// p0315b: runs ONE design-conversation turn as an in-process spec-dialog
/// pipeline run. Seeds the session's transcript, the lazy read-only source
/// sandboxes for the active scope, the dialogue job id (ask_human) and the
/// reply slot into the run via PipelineRequest.Context; pumps the master's
/// questions into the thread while the run is live; owns the sandboxes'
/// lifetime (released when the turn ends — a sandbox that served no read
/// holds nothing). Returns the reply as kept and as shown on the session's platform;
/// the router persists the first and delivers the second.
/// <para>
/// 2026-09-13-ed5a: the project's declared TEMPLATES join that set. The scope's repos come from
/// the session row; the templates come off the resolved project — the two columns
/// SpecDialogSessionMapper rebuilds ActiveScope from carry no template.
/// </para>
/// </summary>
public sealed class SpecDialogTurnRunner(
    SpecDialogTurnScope turnScope,
    ServerContext serverContext,
    ExecutePipelineUseCase pipelineUseCase,
    ISourceScopeSandboxFactory sourceSandboxFactory,
    SpecDialogReadOnlyScopes readOnlyScopes,
    SpecDialogTurnImages images,
    SpecDialogQuestionPump questionPump,
    SpecDialogPendingQuestions pendingQuestions,
    SpecDialogTurnGate gate, TicketTextForConversation ticketText, BoundTicketReaders readers,
    DashboardReadingChannel reading,
    DashboardActivityChannel activity, SpecDialogTurnMaterial material, SpecDialogThreadStore threads,
    SpecDialogTurnEnding ending, SpecDialogProposalContinuity continuity, SpecDialogTurnRunLink runLink,
    DesignTurnResourcesFactory turnResources) : ISpecDialogTurnRunner
{
    /// <summary>2026-11-10-d854c: a turn that throws or is cancelled, before its pipeline starts or inside it, leaves a
    /// Notice turn with its run id (null when no run started); a thrown turn answers null, a cancelled one rethrows.</summary>
    public async Task<SpecDialogTurnResult?> RunTurnAsync(
        ConversationState state, TurnPoster poster, CancellationToken cancellationToken)
    {
        var slot = new SpecDialogReplySlot();
        try
        {
            return await RunAsync(state, poster, slot, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await ending.CancelledAsync(state, slot.RunId);
            throw;
        }
        catch (Exception ex)
        {
            await ending.ThrownAsync(state, ex, slot.RunId);
            return null;
        }
    }

    private async Task<SpecDialogTurnResult> RunAsync(
        ConversationState state, TurnPoster poster, SpecDialogReplySlot slot, CancellationToken cancellationToken)
    {
        var project = turnScope.Project(state.Project);
        var scopeRepos = turnScope.Repos(project, state.Scope);
        // 2026-09-22-2d11b: every scope names the CONVERSATION, so its inner sandbox outlives
        // the turn and the next turn takes it back rather than spawning and cloning again.
        var templates = await readOnlyScopes.OpenAsync(project, state.JobId, cancellationToken); // + 283dc sites
        var sandboxes = SpecDialogTurnSeeds.Sandboxes(scopeRepos,
            r => sourceSandboxFactory.Create(project, r, conversationId: state.JobId), templates);
        await using var resources = turnResources.For(project, scopeRepos, sandboxes); // 4778: given back on every exit

        var seeds = SpecDialogTurnSeeds.Build(
            await continuity.ContinuingAsync(state, cancellationToken), scopeRepos, sandboxes, slot, await images.OfAsync(state.JobId, cancellationToken),
            await material.OfAsync(state.JobId, cancellationToken), await ticketText.HeldAsync(state.JobId, project, DialogTarget.Reporting(state), cancellationToken),
            await readers.ForAsync(state.JobId, project, poster, cancellationToken), project.DesignSources, project, await threads.LoadAsync(state, project.Agent, cancellationToken), poster, resources.Clones);
        var request = new PipelineRequest(
            ProjectName: state.Project, PipelineName: PipelinePresets.SpecDialogName,
            Headless: true, Context: seeds);

        using var pumpCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var pump = await questionPump.StartAsync(state, pumpCts.Token); // positioned before the run
        // Set before the pipeline runs, so what it opens and 2026-09-17-042ee what it DOES both report
        // through the flow. 2026-09-18-2f8b: the turn COMPUTES from here to the finally below, its steps
        // kept for a page arriving mid-turn; the activity scope opens INSIDE the try for a throw.
        using var observing = reading.Observe(state);
        using var publishing = continuity.Observe(state);
        using var linking = runLink.Observe(state); // 2026-11-10-d854c: the grounding step names the run on the row
        var computing = gate.Begin(state.JobId);
        try
        {
            using var reporting = activity.Observe(state, computing);
            var result = await pipelineUseCase.ExecuteAsync(request, serverContext.ConfigPath, cancellationToken);
            return await ending.OfAsync(state, slot, project, templates, result, cancellationToken);
        }
        finally
        {
            gate.Finish(state.JobId, computing);
            pumpCts.Cancel();
            await pump;
            pendingQuestions.Clear(state.JobId);
        }
    }
}
