using AgentSmith.Contracts.Dialogue;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;

namespace AgentSmith.Tests.TestSupport;

/// <summary>A chat platform that records what was posted to which thread, and answers
/// <see cref="ReplyEndpointFor"/> with what the test told it.</summary>
internal sealed class RecordingThreadAdapter(string platform, string? replyEndpoint = null) : IChatThreadAdapter
{
    public string Platform => platform;

    public List<(ChatThread Thread, string Text)> Posts { get; } = [];

    public List<(ChatThread Thread, DialogQuestion Question)> Questions { get; } = [];

    public string? ReplyEndpointFor(string channelId) => replyEndpoint;

    public Task PostAsync(ChatThread thread, string text, CancellationToken cancellationToken)
    {
        lock (Posts) Posts.Add((thread, text));
        return Task.CompletedTask;
    }

    public Task PostQuestionAsync(ChatThread thread, DialogQuestion question, CancellationToken cancellationToken)
    {
        lock (Questions) Questions.Add((thread, question));
        return Task.CompletedTask;
    }
}
