using AgentSmith.Contracts.Dialogue;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// 2026-09-15-9033: the dashboard as a chat platform. Replies and questions are pushed
/// into the SignalR group of the one session they belong to, so a design conversation
/// that files real tickets needs no chat tenant.
/// <para>
/// The hub context is OPTIONAL because the dashboard API is env-gated while this adapter
/// is composed unconditionally (the messenger enumerates every adapter when it is built).
/// With the dashboard switched off nothing can open a session on this platform anyway —
/// its endpoint is mapped by the same gate — and a push that finds no hub says so.
/// </para>
/// <para>
/// This channel carries the spec dialog and nothing else: no run-trigger conversation is
/// created on it, and the cross-adapter dispatchers key by the conversation's stored
/// platform rather than iterating adapters, so the progress / completion / error methods
/// are never dispatched here. They are implemented as what they are — a logged arrival,
/// never a silent success.
/// </para>
/// </summary>
public sealed class DashboardAdapter(
    ILogger<DashboardAdapter> logger,
    IHubContext<JobsHub>? hub = null) : IPlatformAdapter
{
    private const string ReplyTitle = "Spec dialog";

    public string Platform => DispatcherDefaults.PlatformDashboard;

    public Task SendMessageAsync(string channelId, string text, CancellationToken cancellationToken) =>
        SendInfoAsync(channelId, ReplyTitle, text, threadId: null, cancellationToken);

    public Task SendInfoAsync(string channelId, string title, string text,
        string? threadId, CancellationToken cancellationToken) =>
        PushAsync(
            "SpecDialogMessage", Dialog(channelId, threadId),
            id => new SpecDialogChannelMessage(id, title, text, DateTimeOffset.UtcNow),
            cancellationToken);

    /// <summary>
    /// The approval that files tickets reaches a person through this method and no other —
    /// the outcome confirmer never sends its question as an ordinary message. It does NOT
    /// block: a text reply in the session already wins through the router's
    /// pending-question branch, and the returned null leaves that the one answer path.
    /// </summary>
    public async Task<DialogAnswer?> AskTypedQuestionAsync(string channelId,
        DialogQuestion question, string? threadId, CancellationToken cancellationToken)
    {
        await PushAsync(
            "SpecDialogQuestion", Dialog(channelId, threadId),
            id => new SpecDialogChannelQuestion(
                id, question.QuestionId, question.Text,
                question.Choices ?? [], DateTimeOffset.UtcNow),
            cancellationToken);
        return null;
    }

    public Task SendProgressAsync(string channelId, int step, int total, string commandName,
        CancellationToken cancellationToken) =>
        NotCarriedHere(nameof(SendProgressAsync), channelId);

    public Task SendDoneAsync(string channelId, string summary, string? prUrl,
        CancellationToken cancellationToken) =>
        NotCarriedHere(nameof(SendDoneAsync), channelId);

    public Task SendErrorAsync(string channelId, ErrorContext errorContext,
        CancellationToken cancellationToken) =>
        NotCarriedHere(nameof(SendErrorAsync), channelId);

    public Task UpdateQuestionAnsweredAsync(string channelId, string messageId,
        string questionText, string answer, CancellationToken cancellationToken) =>
        NotCarriedHere(nameof(UpdateQuestionAnsweredAsync), channelId);

    public Task SendDetailAsync(string channelId, string text, CancellationToken cancellationToken) =>
        NotCarriedHere(nameof(SendDetailAsync), channelId);

    public Task SendClarificationAsync(string channelId, string suggestion,
        CancellationToken cancellationToken) =>
        NotCarriedHere(nameof(SendClarificationAsync), channelId);

    // The dialog id IS the thread id; the channel id carries it too, because a browser
    // has one conversation per page and no room above it.
    private static string Dialog(string channelId, string? threadId) =>
        string.IsNullOrEmpty(threadId) ? channelId : threadId;

    private async Task PushAsync<TPayload>(string clientMethod, string dialogId,
        Func<string, TPayload> payload, CancellationToken cancellationToken)
    {
        if (hub is null)
        {
            logger.LogWarning(
                "The dashboard API is switched off, so '{ClientMethod}' for spec dialog "
                + "{DialogId} has nowhere to go", clientMethod, dialogId);
            return;
        }
        await hub.Clients.Group(HubGroups.SpecDialog(dialogId))
            .SendAsync(clientMethod, payload(dialogId), cancellationToken);
    }

    // Logged rather than implemented: a run-trigger conversation is never opened on this
    // platform, so an arrival here means something started routing runs through the
    // dashboard channel — which is a finding, not a message to swallow.
    private Task NotCarriedHere(string method, string channelId)
    {
        logger.LogWarning(
            "{Method} was dispatched to the dashboard channel for {ChannelId}, which "
            + "carries the spec dialog only", method, channelId);
        return Task.CompletedTask;
    }
}
