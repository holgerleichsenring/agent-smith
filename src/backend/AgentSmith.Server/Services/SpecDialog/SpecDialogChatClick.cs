using AgentSmith.Contracts.Dialogue;
using AgentSmith.Infrastructure.Persistence.Repositories;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-11-10-ccd9: a button clicked on a conversation's card in a chat thread. The card's question id IS
/// the proposal id, so the click is routed as the clicker's own message carrying that id — an approve or
/// reject decides that card, a shape runs an ordinary turn. A chat click starts no runs: like a chat message,
/// it carries no runs.control.
/// </summary>
public sealed class SpecDialogChatClick(
    SpecDialogSessionManager sessions, SpecDialogProposalClaim claims, SpecDialogRouter router)
{
    /// <summary>A click on the card this thread awaits or was last shown; false leaves it to the run bindings.</summary>
    public async Task<bool> TryCurrentAsync(
        string platform, string channelId, string? threadId, DialogAnswer answer, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(threadId)) return false;
        var row = await claims.ReadAsync(platform, threadId, ct);
        if (answer.QuestionId != row.AwaitingId && answer.QuestionId != row.PostedId) return false;
        return await RouteAsync(platform, channelId, threadId, answer, ct);
    }

    /// <summary>A click no run took, in a thread with an open conversation: an older card, which the decision
    /// path answers by saying so and showing the current one.</summary>
    public async Task<bool> TryStaleAsync(
        string platform, string channelId, string? threadId, DialogAnswer answer, CancellationToken ct) =>
        !string.IsNullOrEmpty(threadId)
        && await sessions.GetOpenByThreadAsync(platform, threadId, ct) is not null
        && await RouteAsync(platform, channelId, threadId, answer, ct);

    private Task<bool> RouteAsync(string platform, string channelId, string threadId, DialogAnswer answer, CancellationToken ct) =>
        router.TryRouteAsync(answer.Answer, answer.AnsweredBy, channelId, threadId, platform,
            mayStartRuns: false, ct, proposalId: answer.QuestionId);
}
