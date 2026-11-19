using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-09-9a98: a proposal outlives the turn that wrote it; the next turn continues from it. 2026-11-19-6b0bd: no
/// draft is published mid-turn any more, so the proposal awaiting a decision is the one continued.
/// </summary>
public sealed class SpecDialogProposalContinuity(SpecDialogLatestOutcomeStore latestOutcome)
{
    /// <summary>2026-11-10-ccd9: the proposal awaiting a decision is what the conversation is about, so the master reads
    /// it and revises it when asked.</summary>
    public async Task<ConversationState> ContinuingAsync(ConversationState state, CancellationToken ct)
    {
        if (state.Revising is not null || state.ThreadId is null) return state;
        var stored = await latestOutcome.ReadAsync(state.Platform, state.ThreadId, ct);
        return stored.Proposal is { } revising ? state with { Revising = revising } : state;
    }
}
