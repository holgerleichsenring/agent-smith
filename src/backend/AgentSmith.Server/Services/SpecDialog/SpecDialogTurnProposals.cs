using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-10-d854c: each kept turn of a conversation's view gets its own proposal, read by the turn's run id, so a
/// later proposal never hides an earlier one. A mark under review stays only while a turn computes.
/// <para>2026-11-11-205ff: NO ORPHAN ROW. A stored proposal no kept turn carries is classified by its run row alone,
/// which every replica sees alike: the newest live one (this conversation's running run first) is
/// <see cref="SpecDialogSessionView.RunningProposal"/>; every other one is listed in
/// <see cref="SpecDialogSessionView.InterruptedTurns"/>, marked running while its row is live — another replica's turn —
/// and as stopped once it is not. Until the liveness reaper marks a dead run's row, that row still reads as live.</para>
/// <para>2026-11-12-01b4: the awaiting proposal sits on the kept turn whose own proposal it is. Since 2026-11-12-cd4ca a
/// draft is written by tools, so no reply's text holds one and the text anchor finds no turn; the card then trailed the
/// transcript as a second copy of the reply's result. The text anchor stays for rows older than d854c.</para>
/// </summary>
public sealed class SpecDialogTurnProposals(
    SpecDialogTurnProposalStore store, SpecDialogProposalComposer composer, RunLivenessRepository runs)
{
    public async Task<SpecDialogSessionView> WithAsync(
        SpecDialogSessionView session, ConversationState state, bool computing, CancellationToken ct,
        OutcomeProposal? awaiting = null)
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
                ? new SpecDialogInterruptedTurnView(row.Key, row.Value.At, push, Running: live.Contains(row.Key)) : null)
            .OfType<SpecDialogInterruptedTurnView>().ToList();
        return session with
        {
            Transcript = transcript,
            ProposalTurn = AwaitingTurn(session, byRun, awaiting) ?? session.ProposalTurn,
            RunningRunId = state.RunningRunId,
            RunningProposal = running.Key is null ? null : Push(state, running.Value.Proposal, state.LastActivityAt, computing),
            InterruptedTurns = interrupted.Count == 0 ? null : interrupted,
        };
    }

    /// <summary>The last kept turn whose own proposal is <paramref name="awaiting"/>: both are written from one object by
    /// one serializer (SpecDialogOutcomeFlow), so equal JSON is the same proposal.</summary>
    private static int? AwaitingTurn(
        SpecDialogSessionView session, IReadOnlyDictionary<string, SpecDialogTurnProposalRead> byRun, OutcomeProposal? awaiting)
    {
        if (awaiting is null || session.Proposal is null) return null;
        var json = OutcomeProposalJson.Write(awaiting);
        for (var index = session.Transcript.Count - 1; index >= 0; index--)
            if (session.Transcript[index].RunId is { } runId && byRun.TryGetValue(runId, out var kept)
                && OutcomeProposalJson.Write(kept.Proposal) == json)
                return index;
        return null;
    }

    private SpecDialogProposalPush? Push(ConversationState state, OutcomeProposal proposal, DateTimeOffset at, bool computing) =>
        composer.Compose(DialogTarget.Of(state), proposal.UnderReview && !computing ? proposal with { UnderReview = false } : proposal, at);
}
