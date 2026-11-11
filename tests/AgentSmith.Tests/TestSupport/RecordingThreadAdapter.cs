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

    /// <summary>2026-11-11-205fh: the time the platform reports for a posted card — Slack's ts by default (now), null
    /// for a platform that gives none.</summary>
    public Func<DateTimeOffset?> PostedAt { get; set; } = () => DateTimeOffset.UtcNow;

    /// <summary>2026-11-11-205fh: a post that throws, as a platform outage does.</summary>
    public bool Fails { get; set; }

    public Task<PostedMessage> PostQuestionAsync(ChatThread thread, DialogQuestion question, CancellationToken cancellationToken)
    {
        if (Fails) throw new HttpRequestException("the platform is down");
        lock (Questions) Questions.Add((thread, question));
        return Task.FromResult(new PostedMessage($"m-{Questions.Count}", PostedAt()));
    }
}
