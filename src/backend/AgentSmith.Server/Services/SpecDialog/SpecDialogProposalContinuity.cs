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

    /// <summary>A stored mark means the turn that wrote this proposal never finished its review.</summary>
    public async Task<ConversationState> ContinuingAsync(ConversationState state, CancellationToken ct)
    {
        if (state.Revising is not null || state.ThreadId is null) return state;
        var stored = (await latestOutcome.ReadAsync(state.Platform, state.ThreadId, ct)).Proposal;
        return stored is { UnderReview: true } ? state with { Revising = stored } : state;
    }
}
