using AgentSmith.Application.Services.Events;
using AgentSmith.Application.Services.Turns;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;
using AgentSmith.Infrastructure.Services.RateLimiting;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.ModelQuota;

/// <summary>
/// 2026-11-22-66e4a: a refusal pauses every call of a run at once, and the run shows ONE waiting line for them, then
/// one line when the last goes on — the Now block's end of the wait.
/// </summary>
public sealed class ModelQuotaWaitLineTests
{
    private readonly AsyncLocalRunContextAccessor _run = new();
    private readonly RecordingEventPublisher _events = new();

    private ModelQuotaWaitLine Line() =>
        new(_run, new AsyncLocalTurnActivityObserverAccessor(), _events, NullLogger<ModelQuotaWaitLine>.Instance);

    [Fact]
    public async Task WaitingAndOver_TwoCallsOfOneRun_ShowOneLineAndEndAfterTheLast()
    {
        var line = Line();
        using var run = _run.BeginScope("run-1");

        await line.WaitingAsync("dep", TimeSpan.FromSeconds(7.2), "the provider refused the call", CancellationToken.None);
        await line.WaitingAsync("dep", TimeSpan.FromSeconds(7.2), "the provider refused the call", CancellationToken.None);
        await line.OverAsync("dep", CancellationToken.None);
        var beforeLast = Details();
        await line.OverAsync("dep", CancellationToken.None);

        beforeLast.Should().ContainSingle().Which.Detail.Should()
            .Be("Waiting 8 s for model quota on dep: the provider refused the call");
        Details().Select(d => d.Origin).Should().Equal(ModelQuotaWaitLine.Origin, ModelQuotaWaitLine.OverOrigin);
    }

    [Fact]
    public async Task RateLimitingChatClient_ARefusedCall_ShowsTheWaitingLineAndItsEnd()
    {
        var line = Line();
        using var run = _run.BeginScope("run-1");
        var limiter = new LlmRateLimiter(new LlmRateLimitOptions(600, 600_000));
        var client = new RateLimitingChatClient(new QuotaScriptedClient(QuotaScriptedClient.TooEarly("0.01")), limiter, "dep",
            new RetryConfig(), new ThrottleWaitReporter(), NullLogger<RateLimitingChatClient>.Instance, waitLine: line);

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        Details().Select(d => d.Origin).Should().Equal(ModelQuotaWaitLine.Origin, ModelQuotaWaitLine.OverOrigin);
        Details()[0].Detail.Should().StartWith("Waiting 1 s for model quota on dep: the provider refused the call");
    }

    private List<L1StepDetailEvent> Details() => _events.Events.OfType<L1StepDetailEvent>().ToList();
}
