using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using AgentSmith.Tests.Factories;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.SpecDialog.Thread;

/// <summary>A dialog loop replays the previous turn's thread, so its first user message is THAT turn's prompt; the
/// compactor must keep the current turn's prompt out of the fold.</summary>
public sealed class CompactingChatClientCurrentPromptTests
{
    private static readonly CompactionConfig Config = new()
    {
        IsEnabled = true, MaxContextTokens = 800, MaxContextTokensTriggerRatio = 1.0, KeepRecentIterations = 3,
    };

    [Fact]
    public async Task CompactingChatClient_ReplayedThread_FoldKeepsTheCurrentTurnPrompt()
    {
        var current = new ChatMessage(ChatRole.User, "CURRENT turn prompt");
        List<ChatMessage> sent = [.. Replayed(rounds: 10), current];
        var inner = new Recording();
        var folded = new List<ChatMessage>();
        var sut = new CompactingChatClient(inner, Config, null,
            (middle, _) => { folded.AddRange(middle); return Task.FromResult("SUMMARY"); }, pinsCurrentPrompt: true);

        await sut.GetResponseAsync(sent);
        for (var i = 10; i < 20; i++) sent.AddRange(Round(i)); // this turn's tool rounds push its prompt out of the tail
        await sut.GetResponseAsync(sent);

        folded.Should().NotContain(current, "the current prompt is pinned, never summarized");
        var view = inner.Forwarded[^1];
        view.Take(3).Should().Equal(sent[0], sent[1], current);
        view[3].Text.Should().Contain("SUMMARY").And.Contain("the ticket above");
        view.Should().ContainSingle(m => ReferenceEquals(m, current));
    }

    [Fact]
    public async Task CompactingChatClient_NoPriorMessages_PinsAsBefore()
    {
        var withCapture = new Recording();
        var without = new Recording();
        var history = Replayed(rounds: 10);

        await new CompactingChatClient(withCapture, Config, null, (_, _) => Task.FromResult("S"), pinsCurrentPrompt: true)
            .GetResponseAsync(history);
        await new CompactingChatClient(without, Config, null, (_, _) => Task.FromResult("S"))
            .GetResponseAsync(history);

        withCapture.Forwarded[^1].Select(m => m.Text).Should().Equal(without.Forwarded[^1].Select(m => m.Text));
    }

    private static List<ChatMessage> Replayed(int rounds)
    {
        var list = new List<ChatMessage> { new(ChatRole.System, "SYSTEM"), new(ChatRole.User, "TURN-1 prompt") };
        for (var i = 0; i < rounds; i++) list.AddRange(Round(i));
        return list;
    }

    private static IEnumerable<ChatMessage> Round(int i) =>
    [
        new(ChatRole.Assistant, $"turn-{i}|" + new string('x', 200)),
        new(ChatRole.User, $"result-{i}|" + new string('y', 200)),
    ];

    private sealed class Recording : IChatClient
    {
        public List<List<ChatMessage>> Forwarded { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            Forwarded.Add(messages.ToList());
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
