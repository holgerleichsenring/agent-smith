using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-10-d854c: each kept turn of a conversation's view gets its own proposal, read by the turn's run id, so a
/// later proposal never hides an earlier one; the running turn's run — after a crash, the turn that died — is named
/// on the session with its proposal when no kept turn carries it. A mark under review stays only while a turn computes.
/// </summary>
public sealed class SpecDialogTurnProposals(SpecDialogTurnProposalStore store, SpecDialogProposalComposer composer)
{
    public async Task<SpecDialogSessionView> WithAsync(
        SpecDialogSessionView session, ConversationState state, bool computing, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        var byRun = await store.ReadAsync(state.JobId, ct);
        var transcript = session.Transcript
            .Select(turn => turn.RunId is { } runId && byRun.TryGetValue(runId, out var proposal)
                ? turn with { Proposal = Push(state, proposal, turn.At, computing) }
                : turn)
            .ToList();
        var running = state.RunningRunId is { } live && byRun.TryGetValue(live, out var draft)
            && transcript.All(turn => turn.RunId != live)
            ? Push(state, draft, state.LastActivityAt, computing)
            : null;
        return session with { Transcript = transcript, RunningRunId = state.RunningRunId, RunningProposal = running };
    }

    private SpecDialogProposalPush? Push(ConversationState state, OutcomeProposal proposal, DateTimeOffset at, bool computing) =>
        composer.Compose(DialogTarget.Of(state), proposal.UnderReview && !computing ? proposal with { UnderReview = false } : proposal, at);
}
