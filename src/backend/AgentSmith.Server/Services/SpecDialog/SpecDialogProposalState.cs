using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-10-ccd9: the proposal a conversation is deciding on, as the session row keeps it. Unlike the
/// pane's display record, every write here fails its caller: a card that was never stored must not be
/// shown, and a claim or a reject that did not land must not be reported as done. A ticket's own
/// conversation is nudged after each change, as a saved turn nudges it.
/// </summary>
public sealed class SpecDialogProposalState(
    SpecDialogProposalClaim claim, SpecDialogSessionRepository sessions, BoundSessionNudge bound)
{
    /// <summary>Stores the turn's final proposal as awaiting under a fresh id; returns it with the id it replaced.</summary>
    public async Task<(string Id, string? Replaced)> StoreAwaitingAsync(
        ConversationState state, OutcomeProposal proposal, CancellationToken ct)
    {
        var stored = await claim.SetAwaitingAsync(state.Platform, state.ThreadId!, OutcomeProposalJson.Write(proposal), ct);
        await NudgeAsync(state, ct);
        return stored;
    }

    /// <summary>A draft under review; never approvable, never over the awaiting proposal.</summary>
    public async Task StoreReviewingAsync(ConversationState state, OutcomeProposal proposal, CancellationToken ct)
    {
        await claim.SetReviewingAsync(state.Platform, state.ThreadId!, OutcomeProposalJson.Write(proposal), ct);
        await NudgeAsync(state, ct);
    }

    /// <summary>The claimed proposal, or null when <paramref name="id"/> is no longer the awaiting one.</summary>
    public async Task<OutcomeProposal?> ClaimAsync(ConversationState state, string id, CancellationToken ct)
    {
        if (await claim.ClaimAsync(state.Platform, state.ThreadId!, id, ct) is not { } json) return null;
        await NudgeAsync(state, ct);
        return OutcomeProposalJson.Read(json);
    }

    public async Task<bool> RejectAsync(ConversationState state, string id, CancellationToken ct)
    {
        if (!await claim.RejectAsync(state.Platform, state.ThreadId!, id, ct)) return false;
        await NudgeAsync(state, ct);
        return true;
    }

    /// <summary>Clears an awaiting proposal the ticket steward took over from; its id, or null when none waited.</summary>
    public async Task<string?> SupersedeAsync(ConversationState state, CancellationToken ct)
    {
        var cleared = await claim.SupersedeAsync(state.Platform, state.ThreadId!, ct);
        if (cleared is not null) await NudgeAsync(state, ct);
        return cleared;
    }

    /// <summary>The awaiting id, and the card last posted to the thread with the moment it was posted.</summary>
    public Task<(string? AwaitingId, string? PostedId, DateTimeOffset? PostedAt)> ReadAsync(
        ConversationState state, CancellationToken ct) => claim.ReadAsync(state.Platform, state.ThreadId!, ct);

    /// <summary>The awaiting proposal itself with its id; null when none awaits.</summary>
    public async Task<(string Id, OutcomeProposal Proposal)?> AwaitingAsync(
        ConversationState state, SpecDialogLatestOutcomeStore outcomes, CancellationToken ct) =>
        await outcomes.ReadAsync(state.Platform, state.ThreadId!, ct) is { ProposalId: { } id, Proposal: { } proposal }
            ? (id, proposal) : null;

    public Task PostedAsync(ConversationState state, string id, DateTimeOffset at, CancellationToken ct) =>
        claim.SetPostedAsync(state.Platform, state.ThreadId!, id, at, ct);

    /// <summary>Gives a row from before the deploy its id; a stored draft under review stays one. An unreadable
    /// row is backfilled as awaiting and then reads as absent, as the pane's reads always treated it.</summary>
    public Task BackfillAsync(string platform, string threadId, CancellationToken ct) =>
        claim.BackfillAsync(platform, threadId, UnderReview, ct);

    private static bool UnderReview(string json)
    {
        try { return OutcomeProposalJson.Read(json).UnderReview; }
        catch (Exception ex) when (ex is System.Text.Json.JsonException or InvalidOperationException or NotSupportedException) { return false; }
    }

    private async Task NudgeAsync(ConversationState state, CancellationToken ct) =>
        await bound.NotifyAsync(await sessions.GetOpenByThreadAsync(state.Platform, state.ThreadId!, ct), ct);
}
