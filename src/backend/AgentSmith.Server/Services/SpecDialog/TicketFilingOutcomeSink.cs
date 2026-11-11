using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog.Continuation;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// p0315c: the real IOutcomeSink — files the confirmed outcome as tracker
/// ticket(s). Durable-first: the claim persisted the proposal on the session BEFORE
/// filing so a tracker failure (or a crash) keeps the confirmed outcome
/// available for a retry; a fully successful filing clears its own (205fe). The thread and
/// the durable dialogue trail both get the ticket references — or the honest
/// failure report naming exactly what WAS created.
/// <para>
/// 2026-10-13-9a59l: a spec or an epic approved in a ticket's OWN conversation files nothing new for
/// an open ticket — it goes to <see cref="TicketContinuationAct"/>, and is answered as a FILING turn,
/// because the conversation has changed work on a tracker.
/// </para>
/// <para>2026-10-14-dbcel: a list of tickets goes to <see cref="TicketGraphFiler"/> — and is refused in a ticket's own
/// conversation, which continues that ticket. Once a ticket exists the rest runs on CancellationToken.None.</para>
/// <para>2026-11-10-ccd9 review: it says whether it acted, so a refused approval goes back to awaiting.</para>
/// </summary>
public sealed class TicketFilingOutcomeSink(
    AgentSmith.Infrastructure.Persistence.Repositories.SpecDialogConfirmedSlot confirmed,
    OutcomeTicketFiler filer,
    TicketGraphFiler graphs,
    TicketContinuationAct continuation,
    SpecDialogSessionManager sessions,
    SpecDialogMessenger messenger,
    SpecDialogOutcomeComposer composer,
    DashboardOutcomeChannel outcomeChannel,
    SpecDialogLatestOutcomeStore latestOutcome,
    ILogger<TicketFilingOutcomeSink> logger) : IOutcomeSink
{
    public async Task<OutcomeAcceptance> AcceptAsync(
        ConversationState state, OutcomeProposal proposal, bool mayStartRuns,
        CancellationToken cancellationToken, string? proposalId = null)
    {
        if (SpecDialogApprovalRefusal.Of(state, proposal) is { } refusal)
        {
            await RefuseAsync(state, refusal, cancellationToken);
            return OutcomeAcceptance.Refused;
        }
        if (state.TicketKey is not null && proposal is SpecOutcome or EpicOutcome or TicketCommentOutcome)
            return await ContinueAsync(state, proposal, mayStartRuns, proposalId, cancellationToken);
        // 2026-11-11-205fe: the claim wrote the confirmed slot with its id; a second writer here was the defect.
        var report = proposal is TicketGraphOutcome graph
            ? await graphs.FileAsync(state, graph, mayStartRuns, cancellationToken)
            : await filer.FileAsync(state, proposal, mayStartRuns, cancellationToken);
        // A created ticket must not lose its report, its notice or its turn to a cancellation.
        var ct = report.Filed.Count > 0 ? CancellationToken.None : cancellationToken;
        if (report.Succeeded)
        {
            await confirmed.ClearAsync(state.Platform, state.ThreadId!, proposalId, ct);
            logger.LogInformation(
                "Filed {Count} ticket(s) for spec-dialog session {SessionId}: {Refs}",
                report.Filed.Count, state.JobId,
                string.Join(", ", report.Filed.Select(t => t.Reference)));
        }

        await latestOutcome.SetFilingAsync(
            state.Platform, state.ThreadId!, report, proposal, ct);

        // Published before the notice is composed: the pane is the record of what was
        // created, and a chat API that cannot be reached must not take it down with it.
        await outcomeChannel.FiledAsync(state, report, ct);

        // The notice is both sent and kept: the transcript is the master's own context, so
        // it holds the line in the dialect the reader of this conversation sees.
        var notice = (report.Succeeded
            ? composer.ComposeFiled(proposal, report)
            : composer.ComposeFilingFailure(report, proposal))
            .In(SpecDialogMarkup.For(state.Platform));
        await messenger.SendAsync(state.Platform, state.ChannelId, state.ThreadId!, notice, ct);
        await sessions.AppendTurnAsync(
            state.Platform, state.ThreadId!, TranscriptRole.Assistant, notice, SpecDialogTurnKind.Filing, null, ct);
        return OutcomeAcceptance.Acted;
    }

    private async Task RefuseAsync(ConversationState state, string notice, CancellationToken cancellationToken)
    {
        logger.LogInformation("An approval in conversation {SessionId} was refused: {Notice}", state.JobId, notice);
        await messenger.SendAsync(state.Platform, state.ChannelId, state.ThreadId!, notice, cancellationToken);
        await sessions.AppendTurnAsync(
            state.Platform, state.ThreadId!, TranscriptRole.Assistant, notice, SpecDialogTurnKind.Notice, null, cancellationToken);
    }

    private async Task<OutcomeAcceptance> ContinueAsync(
        ConversationState state, OutcomeProposal proposal, bool mayStartRuns, string? proposalId,
        CancellationToken cancellationToken)
    {
        var outcome = await continuation.ApplyAsync(state, proposal, mayStartRuns, cancellationToken);
        // 205fe review: an acted continuation clears its own confirmed slot, as a filing does; a refused one keeps it.
        if (outcome.Acted) await confirmed.ClearAsync(state.Platform, state.ThreadId!, proposalId, CancellationToken.None);
        logger.LogInformation(
            "Approval in ticket conversation {SessionId}: {Notice}", state.JobId, outcome.Notice);
        if (outcome is { Filing: { } report, Filed: { } filed })
        {
            await latestOutcome.SetFilingAsync(state.Platform, state.ThreadId!, report, filed, cancellationToken);
            await outcomeChannel.FiledAsync(state, report, cancellationToken);
        }
        // 2026-11-10-ccd9: the claim already freed the awaiting slot; clearing here could only wipe a newer proposal.
        await messenger.SendAsync(
            state.Platform, state.ChannelId, state.ThreadId!, outcome.Notice, cancellationToken);
        await sessions.AppendTurnAsync(
            state.Platform, state.ThreadId!, TranscriptRole.Assistant, outcome.Notice,
            SpecDialogTurnKind.Filing, null, cancellationToken);
        // A refused continuation recorded nothing, so the decision path gives the proposal back to awaiting.
        return outcome.Acted ? OutcomeAcceptance.Acted : OutcomeAcceptance.Refused;
    }
}
