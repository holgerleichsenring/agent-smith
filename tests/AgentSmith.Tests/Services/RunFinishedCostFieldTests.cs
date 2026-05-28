using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Server.Services.Events;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public sealed class RunFinishedCostFieldTests
{
    private const string RunId = "2026-05-28T09-00-00-rfin";

    [Fact]
    public void RunSnapshot_ApplyRunFinishedWithCostUsd_OverridesAccumulatedTotal()
    {
        // Per-call accumulation lands first (e.g. via factory-wrapped decorator).
        var snapshot = RunSnapshot.Empty(RunId)
            .Apply(new LlmCallFinishedEvent(
                RunId, "gpt-4.1", "Lead",
                TokensIn: 1_000_000, TokensOut: 1_000_000,
                CostUsd: 4.12m,
                DurationMs: 1500, Timestamp: DateTimeOffset.UtcNow))
            .Apply(new LlmCallFinishedEvent(
                RunId, "gpt-4.1", "Lead",
                TokensIn: 500_000, TokensOut: 200_000,
                CostUsd: 2.60m,
                DurationMs: 800, Timestamp: DateTimeOffset.UtcNow));
        snapshot.CostUsd.Should().Be(6.72m);
        snapshot.LlmCalls.Should().Be(2);

        // RunFinished arrives with the tracker's truth → overrides accumulation.
        var finished = snapshot.Apply(new RunFinishedEvent(
            RunId, "success", PrUrl: null, Summary: "ok",
            FinishedAt: DateTimeOffset.UtcNow, CostUsd: 7.0m));

        finished.CostUsd.Should().Be(7.0m);
        finished.Status.Should().Be("success");
        finished.LlmCalls.Should().Be(2);
    }

    [Fact]
    public void RunSnapshot_ApplyRunFinishedWithoutCostUsd_KeepsAccumulatedTotal()
    {
        var snapshot = RunSnapshot.Empty(RunId)
            .Apply(new LlmCallFinishedEvent(
                RunId, "gpt-4.1", "Lead",
                TokensIn: 1_000_000, TokensOut: 1_000_000,
                CostUsd: 4.12m,
                DurationMs: 1500, Timestamp: DateTimeOffset.UtcNow));

        var finished = snapshot.Apply(new RunFinishedEvent(
            RunId, "success", PrUrl: null, Summary: "ok",
            FinishedAt: DateTimeOffset.UtcNow));

        finished.CostUsd.Should().Be(4.12m);
    }

    [Fact]
    public void EventEnvelopeSerializer_RunFinishedWithCost_RoundTrips()
    {
        var original = new RunFinishedEvent(
            RunId, "success", PrUrl: "https://example/pr/42",
            Summary: "all good", FinishedAt: new DateTimeOffset(2026, 5, 28, 9, 0, 0, TimeSpan.Zero),
            CostUsd: 5.3729m);

        var envelope = EventEnvelopeSerializer.Serialize(original);
        var roundtripped = EventEnvelopeSerializer.Deserialize(envelope);

        roundtripped.Should().BeOfType<RunFinishedEvent>();
        var rt = (RunFinishedEvent)roundtripped!;
        rt.CostUsd.Should().Be(5.3729m);
        rt.RunId.Should().Be(RunId);
        rt.Status.Should().Be("success");
        rt.PrUrl.Should().Be("https://example/pr/42");
    }

    [Fact]
    public void EventEnvelopeSerializer_RunFinishedWithNullCost_RoundTrips()
    {
        var original = new RunFinishedEvent(
            RunId, "failed", PrUrl: null, Summary: "boom",
            FinishedAt: new DateTimeOffset(2026, 5, 28, 9, 0, 0, TimeSpan.Zero));

        var envelope = EventEnvelopeSerializer.Serialize(original);
        var roundtripped = (RunFinishedEvent)EventEnvelopeSerializer.Deserialize(envelope)!;

        roundtripped.CostUsd.Should().BeNull();
    }
}
