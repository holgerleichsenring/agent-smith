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
    SpecDialogSessionManager sessions,
    SpecDialogMessenger messenger,
    SpecDialogOutcomeComposer composer,
    ILogger<TicketFilingOutcomeSink> logger) : IOutcomeSink
{
    public async Task AcceptAsync(
        ConversationState state, OutcomeProposal proposal, CancellationToken cancellationToken)
    {
        await outcomeStore.SetConfirmedAsync(
            state.Platform, state.ThreadId!, proposal, cancellationToken);

        var report = await filer.FileAsync(state, proposal, cancellationToken);
        if (report.Succeeded)
        {
            await outcomeStore.ClearConfirmedAsync(state.Platform, state.ThreadId!, cancellationToken);
            logger.LogInformation(
                "Filed {Count} ticket(s) for spec-dialog session {SessionId}: {Refs}",
                report.Filed.Count, state.JobId,
                string.Join(", ", report.Filed.Select(t => t.Reference)));
        }

        var notice = report.Succeeded
            ? composer.ComposeFiled(proposal, report.Filed)
            : composer.ComposeFilingFailure(report.Error!, report.Filed);
        await messenger.SendAsync(
            state.Platform, state.ChannelId, state.ThreadId!, notice, cancellationToken);
        await sessions.AppendTurnAsync(
            state.Platform, state.ThreadId!, TranscriptRole.Assistant, notice, cancellationToken);
    }
}
