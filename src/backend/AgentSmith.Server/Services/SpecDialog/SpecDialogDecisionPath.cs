using AgentSmith.Contracts.Models;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>What a message did to the awaiting card: nothing (an ordinary message), decided it, was refused
/// with the card still awaiting, or found the card replaced and was told so.</summary>
public enum SpecDialogDecided
{
    No,
    Decided,
    Refused,
    Replaced,
}

/// <summary>
/// 2026-11-10-ccd9: an approve or reject decides the card the person SAW. A button and the page's composer
/// name it by id; a word typed in a chat thread names the card that thread was last shown, and only while
/// that card is the latest thing the agent said there and the word was sent after the card was posted. An id
/// that is not the awaiting one decides nothing: the thread is told, and a chat thread is shown the current card
/// again. Approve and reject each land as one compare-and-set, so a second decision on the same card — another
/// replica, another surface — finds it gone.
/// <para>Review fixes: a row from before the deploy is backfilled first; an approval the conversation's shape
/// refuses is told before anything is claimed, and one the sink refuses after the claim goes back to awaiting; a
/// bare typed word never decides a proposal that carries open questions — its buttons do.</para>
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
    /// <summary><paramref name="mayStartRuns"/> is the DECIDER's right, never the turn starter's. <paramref name="sentAt"/>
    /// is the platform's own time of a typed chat message; null where the platform gives none.</summary>
    public async Task<SpecDialogDecided> DecideAsync(
        ConversationState state, SpecDialogDecision decision, string? proposalId, bool mayStartRuns, CancellationToken ct,
        DateTimeOffset? sentAt = null)
    {
        await proposals.BackfillAsync(state.Platform, state.ThreadId!, ct);
        var row = await proposals.ReadAsync(state, ct);
        var id = proposalId ?? await TypedInChatAsync(state, row.PostedId, row.PostedAt, sentAt, ct);
        if (id is null) return SpecDialogDecided.No;
        if (id == row.AwaitingId && await ApplyAsync(state, decision, id, mayStartRuns, ct) is { } done) return done;
        await ReplacedAsync(state, ct);
        return SpecDialogDecided.Replaced;
    }

    private async Task<string?> TypedInChatAsync(
        ConversationState state, string? postedId, DateTimeOffset? postedAt, DateTimeOffset? sentAt, CancellationToken ct)
    {
        if (DialogTarget.IsDashboard(state) || postedId is null || postedAt is not { } at) return null;
        if (sentAt is { } sent && sent <= at) return null;
        if (LastAgentEntryAt(state) is { } last && last > at) return null;
        return await proposals.AwaitingAsync(state, outcomes, ct) is { Proposal.Questions.Count: > 0 } ? null : postedId;
    }

    // The agent's own replies count; a framework Notice (a filing, a refusal, a replacement) does not.
    private static DateTimeOffset? LastAgentEntryAt(ConversationState state) =>
        state.Transcript.LastOrDefault(t => t.Role == TranscriptRole.Assistant && t.Kind != SpecDialogTurnKind.Notice)?.At;

    /// <summary>Null when the compare-and-set found the card gone.</summary>
    private async Task<SpecDialogDecided?> ApplyAsync(
        ConversationState state, SpecDialogDecision decision, string id, bool mayStartRuns, CancellationToken ct)
    {
        if (decision == SpecDialogDecision.Rejected)
        {
            if (!await proposals.RejectAsync(state, id, ct)) return null;
            await notices.TellAsync(state, composer.ComposeRejected(), ct);
            return SpecDialogDecided.Decided;
        }
        if (await proposals.AwaitingAsync(state, outcomes, ct) is { } awaiting && awaiting.Id == id
            && SpecDialogApprovalRefusal.Of(state, awaiting.Proposal) is { } refusal)
        {
            await notices.SayAsync(state, refusal, ct);
            return SpecDialogDecided.Refused;
        }
        if (await proposals.ClaimAsync(state, id, ct) is not { } claimed) return null;
        // Only a person's ratification names a mock: stamped from the transcript as it stands now.
        if (await sink.AcceptAsync(state, await ratifiedMocks.StampAsync(state, claimed, ct), mayStartRuns, ct, id) == OutcomeAcceptance.Acted)
            return SpecDialogDecided.Decided;
        await proposals.ReleaseAsync(state, id, ct);
        return SpecDialogDecided.Refused;
    }

    private async Task ReplacedAsync(ConversationState state, CancellationToken ct)
    {
        var current = await proposals.AwaitingAsync(state, outcomes, ct);
        await notices.TellAsync(state, composer.ComposeReplaced(current is not null), ct);
        if (current is { } awaiting) await chatCards.PostAsync(state, awaiting.Proposal, awaiting.Id, ct);
    }
}
