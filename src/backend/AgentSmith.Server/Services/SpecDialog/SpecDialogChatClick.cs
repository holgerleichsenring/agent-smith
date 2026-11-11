using AgentSmith.Contracts.Dialogue;
using AgentSmith.Infrastructure.Persistence.Repositories;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-10-ccd9: a button clicked on a conversation's card in a chat thread. The card's question id IS
/// the proposal id, so the click is routed as the clicker's own message carrying that id — an approve or
/// reject decides that card without waiting on the turn gate, a shape runs an ordinary turn. A chat click starts
/// no runs: like a chat message, it carries no runs.control. Only <see cref="SpecDialogRouted.Decided"/> lets the
/// caller mark the card answered; a refused, replaced or shape click leaves its buttons where they are.
/// </summary>
public sealed class SpecDialogChatClick(
    SpecDialogSessionManager sessions, SpecDialogProposalClaim claims, SpecDialogRouter router)
{
    /// <summary>A click on the card this thread awaits or was last shown; No leaves it to the run bindings.</summary>
    public async Task<SpecDialogRouted> TryCurrentAsync(
        string platform, string channelId, string? threadId, DialogAnswer answer, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(threadId)) return SpecDialogRouted.No;
        var row = await claims.ReadAsync(platform, threadId, ct);
        if (answer.QuestionId != row.AwaitingId && answer.QuestionId != row.PostedId) return SpecDialogRouted.No;
        return await RouteAsync(platform, channelId, threadId, answer, ct);
    }

    /// <summary>A click no run took, in a thread with an open conversation: an older card, which the decision
    /// path answers by saying so and showing the current one.</summary>
    public async Task<SpecDialogRouted> TryStaleAsync(
        string platform, string channelId, string? threadId, DialogAnswer answer, CancellationToken ct) =>
        !string.IsNullOrEmpty(threadId) && await sessions.GetOpenByThreadAsync(platform, threadId, ct) is not null
            ? await RouteAsync(platform, channelId, threadId, answer, ct)
            : SpecDialogRouted.No;

    private Task<SpecDialogRouted> RouteAsync(
        string platform, string channelId, string threadId, DialogAnswer answer, CancellationToken ct) =>
        router.RouteAsync(answer.Answer, answer.AnsweredBy, channelId, threadId, platform,
            mayStartRuns: false, ct, proposalId: answer.QuestionId);
}
