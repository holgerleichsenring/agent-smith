using AgentSmith.Contracts.Dialogue;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.Adapters;

/// <summary>
/// Slack's thread poster: every message is a reply under the thread's ts. A question is posted
/// with the same blocks a live typed question uses, so its buttons come back through the
/// interaction endpoint carrying the question id — nothing here waits for them.
/// </summary>
public sealed class SlackThreadAdapter(
    SlackApiClient api,
    SlackTypedQuestionBlockBuilder questionBlocks) : IChatThreadAdapter
{
    public string Platform => DispatcherDefaults.PlatformSlack;

    public string? ReplyEndpointFor(string channelId) => null;

    public Task PostAsync(ChatThread thread, string text, CancellationToken cancellationToken) =>
        api.PostAsync("chat.postMessage",
            new { channel = thread.ChannelId, text, thread_ts = thread.ThreadId }, cancellationToken);

    public Task PostQuestionAsync(ChatThread thread, DialogQuestion question, CancellationToken cancellationToken) =>
        api.PostAsync("chat.postMessage", new
        {
            channel = thread.ChannelId,
            text = $":thought_balloon: *Question:* {question.Text}",
            blocks = questionBlocks.Build(question),
            thread_ts = thread.ThreadId,
        }, cancellationToken);
}
