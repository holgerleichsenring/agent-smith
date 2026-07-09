using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// p0315e: the honest default IOutcomeSink until p0315c ships ticket filing.
/// Persists the confirmed proposal on the durable SpecDialogSession (the
/// handoff p0315c files from) and posts a summary to the thread that names
/// what WILL be filed — no fake ticket, no pretend URL.
/// </summary>
public sealed class SessionStoreOutcomeSink(
    SpecDialogOutcomeStore outcomeStore,
    SpecDialogMessenger messenger,
    SpecDialogOutcomeComposer composer,
    ILogger<SessionStoreOutcomeSink> logger) : IOutcomeSink
{
    public async Task AcceptAsync(
        ConversationState state, OutcomeProposal proposal, CancellationToken cancellationToken)
    {
        await outcomeStore.SetConfirmedAsync(state.Platform, state.ThreadId!, proposal, cancellationToken);
        logger.LogInformation(
            "Confirmed {Kind} outcome stored on spec-dialog session {SessionId} (filing arrives with p0315c)",
            proposal.GetType().Name, state.JobId);
        await messenger.SendAsync(
            state.Platform, state.ChannelId, state.ThreadId!,
            composer.ComposeStored(state, proposal), cancellationToken);
    }
}
