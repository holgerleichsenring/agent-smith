using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets;
using Microsoft.Extensions.DependencyInjection;
using AgentSmith.Application.Services.Filing;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Server.Services.Acts;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-25-8e51e: an approval in the conversation of a ticket no code attempt has worked — the
/// approved series becomes the one under the ticket, and the ticket's description says what it says.
/// <para>
/// 2026-10-13-9a59l: THE RECORD AND THE BRANCH ARE WHAT A RUN READS, so they are written on every
/// tracker. The description rewrite is a courtesy for whoever reads the ticket: attempted where the
/// tracker supports it, said when refused or failed, never blocking. It still goes FIRST, so the
/// branch set's fingerprint is the text the tracker stored. A ticket a claim holds is refused whole.
/// </para>
/// <para>2026-11-17-e061c: those writes are an 'amend' run's step (TicketAmendmentWrite) with a run id; this class reads,
/// renders and refuses, then launches it through <see cref="IActRun"/>.</para>
/// </summary>
public sealed class TicketAmendment(
    TicketProjects projects,
    IServiceScopeFactory scopeFactory,
    FiledWorkTrackerProjects trackerProjects,
    Infrastructure.Persistence.Repositories.SpecDialogTicketTextRepository ticketText,
    ApprovedSpecSetRecorder approvals,
    SpecTicketRenderer renderer, EpicChildOrderer orderer, FiledSeriesFactory seriesFiling,
    IActRun acts)
{
    /// <summary>What the thread is told, and whether the approved series was recorded.</summary>
    /// <param name="continuation">The record's continuation — null for a plain never-worked hand-off.</param>
    public async Task<TicketAmendmentResult> ApplyAsync(
        ConversationState state, OutcomeProposal proposal, CancellationToken ct, SeriesContinuation? continuation = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        var name = state.Scope?.Project ?? state.Project;
        if (projects.Of(name) is not { } project)
            return new($"Project '{name}' is no longer in the configuration catalog, so this "
                + "conversation's ticket cannot be reached. The approval was not recorded.", false);
        if (await ticketText.GetAsync(state.JobId, ct) is not { TicketId: { Length: > 0 } ticketId })
            return new("This conversation never recorded which ticket it belongs to, so there is "
                + "nothing to record the approval under. The approval was not recorded.", false);
        return await ApplyAsync(project, ticketId, ConversationApproval.Of(state), proposal, ct, continuation);
    }

    /// <summary>
    /// 2026-11-17-dd05c: the same amendment keyed by the ticket — the entry a conversation's approval and a dashboard
    /// edit share. <paramref name="source"/> is who approved it, in which conversation, over which repositories.
    /// </summary>
    public async Task<TicketAmendmentResult> ApplyAsync(ResolvedProject project, string ticketId, ApprovalSource source,
        OutcomeProposal proposal, CancellationToken ct, SeriesContinuation? continuation = null)
    {
        ArgumentNullException.ThrowIfNull(project);
        ArgumentNullException.ThrowIfNull(source);
        // 2026-10-06-03c7c: the ticket keeps the series base its filing minted.
        var previous = await approvals.LoadAsync(project, ticketId, ct);
        var amended = AmendedSpecification.Of(proposal, seriesFiling.KeptOrMinted(previous), source.Conversation,
            new AmendmentRendering(renderer, orderer, seriesFiling), TicketLabelVocabulary.For(project.Tracker));
        if (amended.Error is { } refused) return new($"{refused} The approval was not recorded.", false);
        using (var scope = scopeFactory.CreateScope())
        {
            if (await FiledWorkClaims.LeaseOnAsync(scope, trackerProjects.SharingTrackerWith(project.Name), ticketId, ct) is { } claimed)
                return new($"Ticket {ticketId} {claimed} The approval was not recorded: the series a run works "
                    + "from is not replaced under a run that holds the ticket — approve again when no run holds it.", false);
        }
        return await RunAsync(project, new AmendTicketRequest(source, ticketId, amended.Set, amended.Goal, amended.Region, previous, continuation));
    }

    // 2026-11-17-e061c: the writes are an 'amend' run's step; the run answers what the thread is told.
    private async Task<TicketAmendmentResult> RunAsync(ResolvedProject project, AmendTicketRequest request)
    {
        var slot = new ActSlot<TicketAmendmentResult>();
        var result = await acts.RunAsync(project.Name, PipelinePresets.AmendName, new Dictionary<string, object>
        {
            [ContextKeys.AmendTicketRequest] = request,
            [ContextKeys.ActOutcome] = slot,
        });
        return slot.Value ?? new($"The approval under ticket {request.TicketId} did not complete: the amend run ended first "
            + $"({result.Message}). Check the ticket's approved series before triggering it, and approve again.", false);
    }
}
