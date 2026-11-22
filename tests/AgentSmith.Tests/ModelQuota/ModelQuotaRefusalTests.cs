using System.ClientModel;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.RateLimiting;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using AgentSmith.Tests.TestHelpers;

namespace AgentSmith.Tests.ModelQuota;

/// <summary>
/// 2026-11-22-66e4a: a 429 is the deployment's, not the call's. Measured in production: 28 server-asked waits in four
/// minutes of one run, each lost because sibling sub-agents took the refilled budget first, and calls died when five
/// attempts were spent. Now a refusal pauses every caller of the deployment and the refused call waits it out for as
/// long as the server refuses; a network fault keeps agent.retry.max_retries.
/// </summary>
public sealed class ModelQuotaRefusalTests
{
    private static readonly ModelQuotaScope Scope = new("azure_openai|https://x|dep", "azure_openai", "dep");
    private static readonly RetryConfig Retry = new() { MaxRetries = 2, InitialDelayMs = 1, BackoffMultiplier = 1, MaxDelayMs = 1 };

    private static LlmRateLimiter Limiter() =>
        new(new LlmRateLimitOptions(600, 600_000, LlmRateBudget.RuleFor("azure_openai")));

    private static RateLimitingChatClient Throttled(
        IChatClient inner, ILlmRateLimiter limiter, ThrottleWaitReporter? reporter = null, ILogger<RateLimitingChatClient>? logger = null) =>
        new(inner, limiter, "dep", Retry, reporter ?? new ThrottleWaitReporter(), logger ?? NullLogger<RateLimitingChatClient>.Instance);

    [Fact]
    public async Task Refusal_WithRetryAfter_MakesAConcurrentSecondCallerWaitToo()
    {
        var limiter = Limiter();
        using var cancel = new CancellationTokenSource();
        var refused = new QuotaScriptedClient(QuotaScriptedClient.TooEarly("30"));
        var second = new QuotaScriptedClient();
        var first = Throttled(refused, limiter).GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], null, cancel.Token);
        await TestWaits.UntilAsync(() => limiter.Snapshot(Scope).PausedSeconds > 0, "the refusal to pause the deployment");

        var waiting = Throttled(second, limiter).GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], null, cancel.Token);
        await TestWaits.UntilAsync(() => limiter.Snapshot(Scope).Waiting == 2, "the refused call and the queued second caller to wait");

        second.Attempts.Should().Be(0, "the refusal paused the deployment for every caller, not only the refused one");
        limiter.Snapshot(Scope).PausedSeconds.Should().BeGreaterThan(20, "the pause lasts the server's 30 s");
        cancel.Cancel();
        await FluentActions.Awaiting(() => waiting).Should().ThrowAsync<OperationCanceledException>();
        await FluentActions.Awaiting(() => first).Should().ThrowAsync<OperationCanceledException>();
        refused.Attempts.Should().Be(1, "the refused call waits the server's interval before it is resent");
    }

    [Fact]
    public async Task Refusal_RepeatedPastMaxRetries_IsWaitedOutAndCountsAsThrottleWait()
    {
        var refusals = Enumerable.Range(0, 7).Select(_ => (Exception)QuotaScriptedClient.TooEarly("0.01")).ToArray();
        var inner = new QuotaScriptedClient(refusals);
        var reporter = new ThrottleWaitReporter();
        var client = new TransientRetryChatClient(Throttled(inner, Limiter(), reporter), Retry, "dep", NullLogger.Instance);

        using var scope = reporter.Begin();
        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        response.Text.Should().Be("ok");
        inner.Attempts.Should().Be(8, "a throughput refusal has no attempt cap; max_retries 2 counts network faults");
        scope.WaitedMs.Should().NotBe(0, "the refusal waits reach the call's ThrottleWaitMs");
    }

    [Fact]
    public async Task NetworkFault_RepeatedPastMaxRetries_StillFailsTheCall()
    {
        var drops = Enumerable.Range(0, 4).Select(_ => (Exception)new IOException("drop")).ToArray();
        var inner = new QuotaScriptedClient(drops);
        var client = new TransientRetryChatClient(Throttled(inner, Limiter()), Retry, "dep", NullLogger.Instance);

        await FluentActions.Awaiting(() => client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]))
            .Should().ThrowAsync<IOException>();
        inner.Attempts.Should().Be(3, "the network ladder is unchanged: one call and max_retries 2 retries");
    }

    [Fact]
    public async Task Refusal_ForASpentCredit_IsNotWaitedFor()
    {
        var inner = new QuotaScriptedClient(QuotaScriptedClient.CreditSpent());
        var client = new TransientRetryChatClient(Throttled(inner, Limiter()), Retry, "dep", NullLogger.Instance);

        await FluentActions.Awaiting(() => client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]))
            .Should().ThrowAsync<ClientResultException>();
        inner.Attempts.Should().Be(1, "insufficient_quota says the credit is spent; no wait cures it");
    }

    /// <summary>p0493: the warning names what happened and how long it waits.</summary>
    [Fact]
    public async Task Refusal_LogsTheWaitAndItsReason()
    {
        var logger = new CapturingLogger();
        var inner = new QuotaScriptedClient(QuotaScriptedClient.TooEarly("0.01"));

        await Throttled(inner, Limiter(), logger: logger).GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        logger.Warnings.Should().ContainSingle().Which
            .Should().Contain("rate limit").And.Contain("asked for").And.Contain("every call to it waits");
    }

    private sealed class CapturingLogger : ILogger<RateLimitingChatClient>
    {
        public List<string> Warnings { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning) Warnings.Add(formatter(state, exception));
        }
    }
}
