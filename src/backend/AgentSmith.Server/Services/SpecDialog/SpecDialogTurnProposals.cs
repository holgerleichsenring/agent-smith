using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-10-d854c: each kept turn of a conversation's view gets its own proposal, read by the turn's run id, so a
/// later proposal never hides an earlier one. A mark under review stays only while a turn computes.
/// <para>2026-11-11-205ff: NO ORPHAN ROW. A stored proposal no kept turn carries is either the live turn's — read from
/// its run row, which every replica sees alike — and shown as <see cref="SpecDialogSessionView.RunningProposal"/>, or a
/// turn that stopped before it finished, listed in <see cref="SpecDialogSessionView.InterruptedTurns"/>. Until the
/// liveness reaper marks a dead run's row, that row still reads as live.</para>
/// </summary>
public sealed class SpecDialogTurnProposals(
    SpecDialogTurnProposalStore store, SpecDialogProposalComposer composer, RunLivenessRepository runs)
{
    public async Task<SpecDialogSessionView> WithAsync(
        SpecDialogSessionView session, ConversationState state, bool computing, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(session);
        var byRun = await store.ReadAsync(state.JobId, ct);
        var transcript = session.Transcript
            .Select(turn => turn.RunId is { } runId && byRun.TryGetValue(runId, out var kept)
                ? turn with { Proposal = Push(state, kept.Proposal, turn.At, computing) }
                : turn)
            .ToList();
        var carried = transcript.Select(turn => turn.RunId).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var orphans = byRun.Where(row => !carried.Contains(row.Key)).OrderBy(row => row.Value.At).ToList();
        var live = await runs.LiveAsync([.. orphans.Select(row => row.Key)], ct);
        var running = orphans.Where(row => live.Contains(row.Key))
            .OrderBy(row => row.Key == state.RunningRunId ? 1 : 0).ThenBy(row => row.Value.At).LastOrDefault();
        var interrupted = orphans.Where(row => row.Key != running.Key)
            .Select(row => Push(state, row.Value.Proposal, row.Value.At, computing) is { } push
                ? new SpecDialogInterruptedTurnView(row.Key, row.Value.At, push) : null)
            .OfType<SpecDialogInterruptedTurnView>().ToList();
        return session with
        {
            Transcript = transcript,
            RunningRunId = state.RunningRunId,
            RunningProposal = running.Key is null ? null : Push(state, running.Value.Proposal, state.LastActivityAt, computing),
            InterruptedTurns = interrupted.Count == 0 ? null : interrupted,
        };
    }

    private SpecDialogProposalPush? Push(ConversationState state, OutcomeProposal proposal, DateTimeOffset at, bool computing) =>
        composer.Compose(DialogTarget.Of(state), proposal.UnderReview && !computing ? proposal with { UnderReview = false } : proposal, at);
}
