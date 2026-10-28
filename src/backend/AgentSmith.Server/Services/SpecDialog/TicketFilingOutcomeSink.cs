using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog.Continuation;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// p0315c: the real IOutcomeSink — files the confirmed outcome as tracker
/// ticket(s). Durable-first: the proposal is persisted on the session BEFORE
/// filing so a tracker failure (or a crash) keeps the confirmed outcome
/// available for a retry; a fully successful filing clears it. The thread and
/// the durable dialogue trail both get the ticket references — or the honest
/// failure report naming exactly what WAS created.
/// <para>
/// 2026-10-13-9a59l: a spec or an epic approved in a ticket's OWN conversation files nothing new for
/// an open ticket — it goes to <see cref="TicketContinuationAct"/>, and is answered as a FILING turn,
/// because the conversation has changed work on a tracker.
/// </para>
/// <para>2026-10-14-dbcel: a list of tickets goes to <see cref="TicketGraphFiler"/> — and is refused in a ticket's own
/// conversation, which continues that ticket. Once a ticket exists the rest runs on CancellationToken.None.</para>
/// </summary>
public sealed class TicketFilingOutcomeSink(
    SpecDialogOutcomeStore outcomeStore,
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
    public async Task AcceptAsync(
        ConversationState state, OutcomeProposal proposal, bool mayStartRuns,
        CancellationToken cancellationToken)
    {
        if (state.TicketKey is null && proposal is TicketCommentOutcome) // 2026-10-28-7a69c
        {
            await RefuseAsync(state, UnboundComment, cancellationToken);
            return;
        }
        if (state.TicketKey is not null && proposal is SpecOutcome or EpicOutcome or TicketGraphOutcome or TicketCommentOutcome)
        {
            await (proposal is TicketGraphOutcome
                ? RefuseAsync(state, BoundList(state), cancellationToken) : ContinueAsync(state, proposal, mayStartRuns, cancellationToken));
            return;
        }
        await outcomeStore.SetConfirmedAsync(
            state.Platform, state.ThreadId!, proposal, cancellationToken);

        var report = proposal is TicketGraphOutcome graph
            ? await graphs.FileAsync(state, graph, mayStartRuns, cancellationToken)
            : await filer.FileAsync(state, proposal, mayStartRuns, cancellationToken);
        // A created ticket must not lose its report, its notice or its turn to a cancellation.
        var ct = report.Filed.Count > 0 ? CancellationToken.None : cancellationToken;
        if (report.Succeeded)
        {
            await outcomeStore.ClearConfirmedAsync(state.Platform, state.ThreadId!, ct);
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
    }

    private const string UnboundComment = "A comment is posted on the ticket a conversation belongs to, and this one "
        + "belongs to none. Open Work it out on the ticket to comment on it.";

    private static string BoundList(ConversationState state) => $"This conversation belongs to {state.TicketKey}; approving "
        + "here continues that ticket. File a list of tickets from Work it out without a ticket, or make this one ticket's series.";

    private async Task RefuseAsync(ConversationState state, string notice, CancellationToken cancellationToken)
    {
        logger.LogInformation("An approval in conversation {SessionId} was refused: {Notice}", state.JobId, notice);
        await messenger.SendAsync(state.Platform, state.ChannelId, state.ThreadId!, notice, cancellationToken);
        await sessions.AppendTurnAsync(
            state.Platform, state.ThreadId!, TranscriptRole.Assistant, notice, SpecDialogTurnKind.Notice, null, cancellationToken);
    }

    private async Task ContinueAsync(
        ConversationState state, OutcomeProposal proposal, bool mayStartRuns, CancellationToken cancellationToken)
    {
        var outcome = await continuation.ApplyAsync(state, proposal, mayStartRuns, cancellationToken);
        logger.LogInformation(
            "Approval in ticket conversation {SessionId}: {Notice}", state.JobId, outcome.Notice);
        if (outcome is { Filing: { } report, Filed: { } filed })
        {
            await latestOutcome.SetFilingAsync(state.Platform, state.ThreadId!, report, filed, cancellationToken);
            await outcomeChannel.FiledAsync(state, report, cancellationToken);
        }
        // An acted-on proposal is over: the pane, the ticket's conversation and the preview must not offer it again.
        if (outcome.Acted)
            await latestOutcome.ClearProposalAsync(state.Platform, state.ThreadId!, cancellationToken);
        await messenger.SendAsync(
            state.Platform, state.ChannelId, state.ThreadId!, outcome.Notice, cancellationToken);
        await sessions.AppendTurnAsync(
            state.Platform, state.ThreadId!, TranscriptRole.Assistant, outcome.Notice,
            SpecDialogTurnKind.Filing, null, cancellationToken);
    }
}
