using AgentSmith.Contracts.Turns;
using AgentSmith.Server.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-09-9a98: a proposal outlives the turn that wrote it. While the turn runs its review, the
/// proposal is published as it stands; the next turn after an interruption continues from it.
/// </summary>
public sealed class SpecDialogProposalContinuity(
    IServiceScopeFactory scopes,
    ITurnProposalObserverAccessor proposals,
    SpecDialogLatestOutcomeStore latestOutcome,
    ILoggerFactory loggers)
{
    /// <summary>Publishes this turn's proposal until the handle is disposed; null with no thread to store it on.</summary>
    public IDisposable? Observe(ConversationState state) =>
        state.ThreadId is null
            ? null
            : proposals.Observe(new DialogProposalObserver(scopes, state, loggers.CreateLogger<DialogProposalObserver>()));

    /// <summary>
    /// A draft under review means the turn that wrote it never finished; 2026-11-10-ccd9: otherwise the proposal
    /// awaiting a decision is what the conversation is about, so the master reads it and revises it when asked.
    /// </summary>
    public async Task<ConversationState> ContinuingAsync(ConversationState state, CancellationToken ct)
    {
        if (state.Revising is not null || state.ThreadId is null) return state;
        var stored = await latestOutcome.ReadAsync(state.Platform, state.ThreadId, ct);
        return (stored.Reviewing ?? stored.Proposal) is { } revising ? state with { Revising = revising } : state;
    }
}
