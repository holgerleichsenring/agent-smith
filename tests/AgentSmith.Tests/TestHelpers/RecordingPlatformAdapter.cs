using AgentSmith.Contracts.Dialogue;
using AgentSmith.Server.Contracts;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>Records what reached one platform, so a reply on the wrong one is visible.</summary>
internal sealed class RecordingPlatformAdapter(string platform) : IPlatformAdapter
{
    public string Platform { get; } = platform;

    public List<string> Sent { get; } = [];

    public Task SendMessageAsync(string channelId, string text, CancellationToken ct)
    {
        Sent.Add(text);
        return Task.CompletedTask;
    }

    public Task<DialogAnswer?> AskTypedQuestionAsync(
        string channelId, DialogQuestion question, string? threadId, CancellationToken ct) =>
        Task.FromResult<DialogAnswer?>(null);

    public Task SendInfoAsync(
        string channelId, string title, string text, string? threadId, CancellationToken ct) =>
        Task.CompletedTask;

    public Task UpdateQuestionAnsweredAsync(
        string channelId, string messageId, string questionText, string answer,
        CancellationToken ct) => Task.CompletedTask;

    public Task SendClarificationAsync(string channelId, string suggestion, CancellationToken ct)
    {
        Sent.Add(suggestion);
        return Task.CompletedTask;
    }
}
