using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-10-ccd9: an approve or reject decides the card the person SAW. A button and the page's composer
/// name it by id; a word typed in a chat thread names the card that thread was last shown, and only while
/// that card is the latest thing the agent said there. An id that is not the awaiting one decides nothing:
/// the thread is told, and a chat thread is shown the current card again. Approve and reject each land as one
/// compare-and-set, so a second decision on the same card — another replica, another surface — finds it gone.
/// </summary>
public sealed class SpecDialogDecisionPath(
    SpecDialogProposalState proposals,
    SpecDialogLatestOutcomeStore outcomes,
    IOutcomeSink sink,
    RatifiedMockStamp ratifiedMocks,
    SpecDialogOutcomeComposer composer,
    SpecDialogNotices notices,
    SpecDialogChatCard chatCards)
{
    /// <summary>True when the message was a decision on a card (taken or told it is gone); false leaves it an
    /// ordinary message. <paramref name="mayStartRuns"/> is the DECIDER's right, never the turn starter's.</summary>
    public async Task<bool> DecideAsync(
        ConversationState state, SpecDialogDecision decision, string? proposalId, bool mayStartRuns, CancellationToken ct)
    {
        var row = await proposals.ReadAsync(state, ct);
        var id = proposalId ?? TypedInChat(state, row.PostedId, row.PostedAt);
        if (id is null) return false;
        if (id != row.AwaitingId || !await ApplyAsync(state, decision, id, mayStartRuns, ct))
            await ReplacedAsync(state, ct);
        return true;
    }

    private static string? TypedInChat(ConversationState state, string? postedId, DateTimeOffset? postedAt) =>
        DialogTarget.IsDashboard(state) || postedId is null || postedAt is not { } at
            ? null
            : LastAgentEntryAt(state) is { } last && last > at ? null : postedId;

    // The agent's own replies count; a framework Notice (a filing, a refusal, a replacement) does not.
    private static DateTimeOffset? LastAgentEntryAt(ConversationState state) =>
        state.Transcript.LastOrDefault(t => t.Role == TranscriptRole.Assistant && t.Kind != SpecDialogTurnKind.Notice)?.At;

    private async Task<bool> ApplyAsync(
        ConversationState state, SpecDialogDecision decision, string id, bool mayStartRuns, CancellationToken ct)
    {
        if (decision == SpecDialogDecision.Rejected)
        {
            if (!await proposals.RejectAsync(state, id, ct)) return false;
            await notices.TellAsync(state, composer.ComposeRejected(), ct);
            return true;
        }
        if (await proposals.ClaimAsync(state, id, ct) is not { } claimed) return false;
        // Only a person's ratification names a mock: stamped from the transcript as it stands now.
        await sink.AcceptAsync(state, await ratifiedMocks.StampAsync(state, claimed, ct), mayStartRuns, ct);
        return true;
    }

    private async Task ReplacedAsync(ConversationState state, CancellationToken ct)
    {
        var current = await proposals.AwaitingAsync(state, outcomes, ct);
        await notices.TellAsync(state, composer.ComposeReplaced(current is not null), ct);
        if (current is { } awaiting) await chatCards.PostAsync(state, awaiting.Proposal, awaiting.Id, ct);
    }
}
