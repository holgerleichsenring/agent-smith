using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// p0315c: the real IOutcomeSink — files the confirmed outcome as tracker
/// ticket(s). Durable-first: the proposal is persisted on the session BEFORE
/// filing so a tracker failure (or a crash) keeps the confirmed outcome
/// available for a retry; a fully successful filing clears it. The thread and
/// the durable dialogue trail both get the ticket references — or the honest
/// failure report naming exactly what WAS created.
/// </summary>
public sealed class TicketFilingOutcomeSink(
    SpecDialogOutcomeStore outcomeStore,
    OutcomeTicketFiler filer,
    TicketAmendment amendment,
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
        await outcomeStore.SetConfirmedAsync(
            state.Platform, state.ThreadId!, proposal, cancellationToken);

        var report = await filer.FileAsync(state, proposal, mayStartRuns, cancellationToken);
        if (report.Succeeded)
        {
            await outcomeStore.ClearConfirmedAsync(state.Platform, state.ThreadId!, cancellationToken);
            logger.LogInformation(
                "Filed {Count} ticket(s) for spec-dialog session {SessionId}: {Refs}",
                report.Filed.Count, state.JobId,
                string.Join(", ", report.Filed.Select(t => t.Reference)));
        }

        await latestOutcome.SetFilingAsync(
            state.Platform, state.ThreadId!, report, proposal, cancellationToken);

        // Published before the notice is composed: the pane is the record of what was
        // created, and a chat API that cannot be reached must not take it down with it.
        await outcomeChannel.FiledAsync(state, report, cancellationToken);

        // The notice is both sent and kept: the transcript is the master's own context, so
        // it holds the line in the dialect the reader of this conversation sees.
        var notice = (report.Succeeded
            ? composer.ComposeFiled(proposal, report)
            : composer.ComposeFilingFailure(report))
            .In(SpecDialogMarkup.For(state.Platform));
        await messenger.SendAsync(
            state.Platform, state.ChannelId, state.ThreadId!, notice, cancellationToken);
        await sessions.AppendTurnAsync(
            state.Platform, state.ThreadId!, TranscriptRole.Assistant, notice,
            SpecDialogTurnKind.Filing, null, cancellationToken);
    }

    /// <summary>
    /// 2026-09-25-8e51e: the amendment. Nothing is stored as CONFIRMED first — the durable-first
    /// dance above exists so a tracker failure keeps an unfiled outcome retryable, while an
    /// amendment that fails has changed nothing and the proposal is still on the session.
    /// <para>
    /// It is recorded as a FILING turn: the conversation has now changed work on a tracker, which
    /// is exactly the fact the prompt's filed-work clause reads off the transcript.
    /// </para>
    /// </summary>
    public async Task AmendAsync(
        ConversationState state, OutcomeProposal proposal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(state);
        var notice = await amendment.ApplyAsync(state, proposal, cancellationToken);
        logger.LogInformation(
            "Amendment for spec-dialog session {SessionId}: {Notice}", state.JobId, notice);
        await messenger.SendAsync(
            state.Platform, state.ChannelId, state.ThreadId!, notice, cancellationToken);
        await sessions.AppendTurnAsync(
            state.Platform, state.ThreadId!, TranscriptRole.Assistant, notice,
            SpecDialogTurnKind.Filing, null, cancellationToken);
    }
}
