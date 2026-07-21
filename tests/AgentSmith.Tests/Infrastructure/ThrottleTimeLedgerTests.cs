using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.RateLimiting;
using AgentSmith.Server.Services.Events;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Infrastructure;

/// <summary>
/// p0363: the wall-time ledger — the rate limiter's measured wait travels up to
/// the LlmCallFinished event and accumulates on the run snapshot, so "was that
/// hour real work or waiting?" is answered per run instead of estimated.
/// </summary>
public sealed class ThrottleTimeLedgerTests
{
    [Fact]
    public void ThrottleWaitReporter_ReportsInsideScope_AttributesToThatScope()
    {
        ThrottleWaitReporter.Report(999); // outside any scope: no-op, must not throw

        using var scope = ThrottleWaitReporter.Begin();
        ThrottleWaitReporter.Report(120);
        ThrottleWaitReporter.Report(30);
        scope.WaitedMs.Should().Be(150);
    }

    [Fact]
    public void ThrottleWaitReporter_NestedScope_AttributesToInnerNotOuter()
    {
        // A compaction-summarizer call nested inside a master call must charge its
        // own scope — the outer call's split stays honest.
        using var outer = ThrottleWaitReporter.Begin();
        ThrottleWaitReporter.Report(100);
        using (var inner = ThrottleWaitReporter.Begin())
        {
            ThrottleWaitReporter.Report(40);
            inner.WaitedMs.Should().Be(40);
        }
        ThrottleWaitReporter.Report(5);
        outer.WaitedMs.Should().Be(105);
    }

    [Fact]
    public async Task RateLimitingChatClient_ReportsWaitIntoAmbientScope()
    {
        var inner = new EchoChatClient();
        var sut = new RateLimitingChatClient(
            inner, new DelayingLimiter(delayMs: 60), "test",
            NullLogger<RateLimitingChatClient>.Instance);

        using var scope = ThrottleWaitReporter.Begin();
        await sut.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        scope.WaitedMs.Should().BeGreaterThanOrEqualTo(40, "the limiter's acquire wait is reported upward");
    }

    [Fact]
    public void RunSnapshot_AccumulatesLlmDurationAndThrottleWait()
    {
        var snapshot = RunSnapshot.Empty("run-1")
            .Apply(Finished(durationMs: 20_000, throttleMs: 8_000))
            .Apply(Finished(durationMs: 5_000, throttleMs: 0));

        snapshot.LlmDurationMs.Should().Be(25_000);
        snapshot.ThrottleWaitMs.Should().Be(8_000);
        snapshot.LlmCalls.Should().Be(2);
    }

    private static LlmCallFinishedEvent Finished(long durationMs, long throttleMs) =>
        new("run-1", "gpt-5.1", "coder", 1000, 100, 0.01m, durationMs,
            DateTimeOffset.UtcNow, ThrottleWaitMs: throttleMs);

    private sealed class DelayingLimiter(int delayMs) : ILlmRateLimiter
    {
        public async Task<IDisposable> AcquireAsync(int estimatedInputTokens, CancellationToken cancellationToken)
        {
            await Task.Delay(delayMs, cancellationToken);
            return new Noop();
        }

        private sealed class Noop : IDisposable
        {
            public void Dispose() { }
        }
    }

    private sealed class EchoChatClient : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
