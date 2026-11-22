using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Turns;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.RateLimiting;
using AgentSmith.Tests.Services;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.ModelQuota;

/// <summary>
/// 2026-11-22-ab45: a call that cannot fit a minute's quota does not wait in silence. It says so once on the run and in
/// the log, with the numbers and the config key, and keeps waiting and retrying exactly as before.
/// </summary>
public sealed class ModelQuotaCannotFitTests
{
    private readonly AsyncLocalRunContextAccessor _run = new();
    private readonly RecordingEventPublisher _events = new();

    private ModelQuotaWaitLine Line() =>
        new(_run, new AsyncLocalTurnActivityObserverAccessor(), _events, NullLogger<ModelQuotaWaitLine>.Instance);

    [Fact]
    public async Task OversizeCall_IsAdmitted_AndItsRunIsToldOnceWithTheNumbersAndTheKey()
    {
        var line = Line();
        using var run = _run.BeginScope("run-1");
        ChatMessage[] large = [new(ChatRole.User, new string('x', 32_000))];

        foreach (var _ in Enumerable.Range(0, 2))
            await Client(new QuotaScriptedClient(), new LlmRateLimiter(new LlmRateLimitOptions(60, 6_000)), line)
                .GetResponseAsync(large, new ChatOptions { MaxOutputTokens = 500 });

        CannotFit().Should().ContainSingle().Which.Detail.Should()
            .Contain("~8000 tokens").And.Contain("agent.rate_limit.input_tokens_per_minute 6000").And.Contain("keeps waiting");
    }

    [Fact]
    public async Task RefusalAskingLongerThanAMinute_IsToldOnceInTheProvidersWords_AndStillRetried()
    {
        var line = Line();
        using var run = _run.BeginScope("run-1");
        var inner = new QuotaScriptedClient(DailyQuota(), DailyQuota());

        var response = await Client(inner, new InstantLimiter(), line).GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        response.Text.Should().Be("ok");
        inner.Attempts.Should().Be(3, "the call keeps retrying; nothing caps it");
        CannotFit().Should().ContainSingle().Which.Detail.Should()
            .Contain("asks to wait 3600 s").And.Contain("Daily token quota exhausted");
    }

    [Fact]
    public async Task ACallThatCountedMoreThanAMinute_IsToldOnce_AndItsAnswerIsKept()
    {
        var line = Line();
        using var run = _run.BeginScope("run-1");
        var inner = new QuotaScriptedClient { Usage = new UsageDetails { InputTokenCount = 9_000 } };

        var response = await Client(inner, new LlmRateLimiter(new LlmRateLimitOptions(60, 6_000)), line)
            .GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        response.Text.Should().Be("ok");
        CannotFit().Should().ContainSingle().Which.Detail.Should()
            .Contain("used ~9000 tokens").And.Contain("agent.rate_limit.input_tokens_per_minute 6000");
    }

    [Fact]
    public void RecentlyToldRuns_TwoRunsTakingTurns_AreEachToldOnce()
    {
        var told = new RecentlyToldRuns();

        var answers = new[] { "a", "b", "a", "b" }.Select(run => told.FirstTime("dep", run)).ToList();

        answers.Should().Equal(true, true, false, false);
    }

    [Fact]
    public void ProviderRefusalReader_ABodyThatIsNotJson_HasNoWords() =>
        new ProviderRefusalReader(NullLogger.Instance)
            .Words(new System.ClientModel.ClientResultException("x", new FakeResponse(429) { Body = "<html>busy</html>" }))
            .Should().BeNull();

    private RateLimitingChatClient Client(IChatClient inner, ILlmRateLimiter limiter, ModelQuotaWaitLine line) =>
        new(inner, limiter, "dep", new RetryConfig(), new ThrottleWaitReporter(), NullLogger<RateLimitingChatClient>.Instance,
            waitLine: line);

    private List<L1StepDetailEvent> CannotFit() =>
        _events.Events.OfType<L1StepDetailEvent>().Where(e => e.Origin == ModelQuotaWaitLine.CannotFitOrigin).ToList();

    private static System.ClientModel.ClientResultException DailyQuota() =>
        new("rate_limit_exceeded", new FakeResponse(429, ("Retry-After", "3600"))
        {
            Body = """{"error":{"code":"429","message":"Daily token quota exhausted."}}""",
        });

    /// <summary>Admits at once and waits a refusal out at once, so the test does not sit out the ceiling.</summary>
    private sealed class InstantLimiter : ILlmRateLimiter
    {
        public Task<ILlmQuotaLease> AcquireAsync(LlmQuotaDemand demand, CancellationToken cancellationToken) =>
            Task.FromResult<ILlmQuotaLease>(new Lease());

        private sealed class Lease : ILlmQuotaLease
        {
            public Task RefusedAsync(TimeSpan wait, CancellationToken cancellationToken) => Task.CompletedTask;
        }
    }
}
