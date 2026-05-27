using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Events;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Events;

/// <summary>
/// p0169e security boundary: LlmCallStarted / LlmCallFinished carry the
/// sha256-hex-8 of the resolved prompt body; the plaintext prompt must
/// never appear in the event payload. The hash is for correlation +
/// audit; prompt content stays in the cost-summary / result.md path.
/// </summary>
public sealed class PromptHashTests
{
    [Fact]
    public async Task SamePrompt_ProducesSameHashPrefix()
    {
        var recorder = new RecordingEventPublisher();
        var client = NewClient(recorder);
        var prompt = "Resolve the build error in src/Foo.cs";

        await CallAsync(client, prompt);
        await CallAsync(client, prompt);

        var hashes = recorder.Events.OfType<LlmCallStartedEvent>().Select(e => e.PromptHash).ToArray();
        hashes.Should().HaveCount(2);
        hashes[0].Should().Be(hashes[1]);
        hashes[0].Should().MatchRegex("^[0-9a-f]{8}$");
    }

    [Fact]
    public async Task DifferentPrompts_ProduceDifferentHashes()
    {
        var recorder = new RecordingEventPublisher();
        var client = NewClient(recorder);

        await CallAsync(client, "prompt one");
        await CallAsync(client, "prompt two");

        var hashes = recorder.Events.OfType<LlmCallStartedEvent>().Select(e => e.PromptHash).ToArray();
        hashes[0].Should().NotBe(hashes[1]);
    }

    [Fact]
    public async Task PlaintextPromptNeverSerialisedIntoLlmCallEvents()
    {
        var recorder = new RecordingEventPublisher();
        var client = NewClient(recorder);
        const string sensitive = "API_KEY=AKIAIOSFODNN7EXAMPLE-do-not-leak";

        await CallAsync(client, sensitive);

        var events = recorder.Events.OfType<LlmCallStartedEvent>().ToList();
        events.Should().NotBeEmpty();
        foreach (var e in events)
        {
            e.PromptHash.Should().NotContain(sensitive);
            // RunId / Model / Role / Hash + timestamp — none should carry plaintext.
            (e.Model + e.Role + e.RunId + e.PromptHash).Should().NotContain("AKIAIOSFODNN7");
        }
        recorder.Events.OfType<LlmCallFinishedEvent>().Should()
            .OnlyContain(e => !e.ToString()!.Contains(sensitive));
    }

    private static EventPublishingChatClient NewClient(IEventPublisher publisher) =>
        new(new StubChat(), publisher, new ScopedRunContext("2026-05-27T12-00-00-cccc"), role: "Lead");

    private static Task CallAsync(EventPublishingChatClient client, string prompt) =>
        client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, prompt) }, options: null, CancellationToken.None);

    private sealed class StubChat : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class ScopedRunContext(string runId) : IRunContextAccessor
    {
        public string? CurrentRunId => runId;
        public IDisposable BeginScope(string id) => new NoOpScope();
        private sealed class NoOpScope : IDisposable { public void Dispose() { } }
    }
}
