using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using AgentSmith.Infrastructure.Services.Providers.Agent.Compaction;
using FluentAssertions;

namespace AgentSmith.Tests.Compaction;

/// <summary>
/// p0147c: dynamic compaction trigger. Token-pressure is the primary signal;
/// the iteration cap is a defensive backstop. Both compactors share the same
/// predicate semantics.
/// </summary>
public sealed class ShouldCompactTriggerTests
{
    private static CompactionConfig DefaultConfig() => new()
    {
        IsEnabled = true,
        ThresholdIterations = 8,
        MaxContextTokens = 80_000,
        MaxContextTokensTriggerRatio = 0.7
    };

    [Fact]
    public void TokenPressureCrosses_FiresBeforeIterationCap_Claude()
    {
        var config = DefaultConfig();
        // 80_000 × 0.7 = 56_000 — crossing 60k tokens at iter=2 should fire.
        ClaudeContextCompactor.ShouldCompact(currentIterations: 2, estimatedAccumulatedTokens: 60_000, config)
            .Should().BeTrue();
    }

    [Fact]
    public void TokenPressureCrosses_FiresBeforeIterationCap_OpenAi()
    {
        var config = DefaultConfig();
        OpenAiContextCompactor.ShouldCompact(currentIterations: 2, estimatedAccumulatedTokens: 60_000, config)
            .Should().BeTrue();
    }

    [Fact]
    public void IterationCapStillFires_WhenTokensStayLow_Claude()
    {
        var config = DefaultConfig();
        // Tokens well below the 56k trigger — only iteration cap can fire.
        ClaudeContextCompactor.ShouldCompact(currentIterations: 8, estimatedAccumulatedTokens: 1_000, config)
            .Should().BeTrue();
    }

    [Fact]
    public void IterationCapStillFires_WhenTokensStayLow_OpenAi()
    {
        var config = DefaultConfig();
        OpenAiContextCompactor.ShouldCompact(currentIterations: 8, estimatedAccumulatedTokens: 1_000, config)
            .Should().BeTrue();
    }

    [Fact]
    public void BothLow_DoesNotFire_Claude()
    {
        var config = DefaultConfig();
        ClaudeContextCompactor.ShouldCompact(currentIterations: 3, estimatedAccumulatedTokens: 1_000, config)
            .Should().BeFalse();
    }

    [Fact]
    public void BothLow_DoesNotFire_OpenAi()
    {
        var config = DefaultConfig();
        OpenAiContextCompactor.ShouldCompact(currentIterations: 3, estimatedAccumulatedTokens: 1_000, config)
            .Should().BeFalse();
    }

    [Fact]
    public void DisabledConfig_NeverFires_Claude()
    {
        var config = DefaultConfig();
        config.IsEnabled = false;
        ClaudeContextCompactor.ShouldCompact(currentIterations: 1_000, estimatedAccumulatedTokens: 1_000_000, config)
            .Should().BeFalse();
    }

    [Fact]
    public void RatioZero_FallsBackToIterationCapOnly_Claude()
    {
        var config = DefaultConfig();
        config.MaxContextTokensTriggerRatio = 0;
        // Massive token count, low iteration → does NOT fire when ratio is disabled.
        ClaudeContextCompactor.ShouldCompact(currentIterations: 2, estimatedAccumulatedTokens: 1_000_000, config)
            .Should().BeFalse();
        // But iteration cap still fires.
        ClaudeContextCompactor.ShouldCompact(currentIterations: 8, estimatedAccumulatedTokens: 100, config)
            .Should().BeTrue();
    }

    [Fact]
    public void RatioZero_FallsBackToIterationCapOnly_OpenAi()
    {
        var config = DefaultConfig();
        config.MaxContextTokensTriggerRatio = 0;
        OpenAiContextCompactor.ShouldCompact(currentIterations: 2, estimatedAccumulatedTokens: 1_000_000, config)
            .Should().BeFalse();
        OpenAiContextCompactor.ShouldCompact(currentIterations: 8, estimatedAccumulatedTokens: 100, config)
            .Should().BeTrue();
    }
}
