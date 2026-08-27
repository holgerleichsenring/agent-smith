using AgentSmith.Application.Services.Preflight.Checks;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Preflight;

/// <summary>
/// 2026-08-27-3eb1: a compaction threshold that cannot fire before the provider refuses
/// is arithmetic, so it is checkable at startup for nothing — and it is exactly the state
/// an installation was in when four runs in a row died against a 128000 ceiling.
/// </summary>
public sealed class ContextWindowThresholdCheckTests
{
    [Fact]
    public async Task Preflight_AThresholdAtOrAboveTheWindow_IsReported()
    {
        var result = await Run(window: 128000, maxContextTokens: 200000, ratio: 0.7);

        result.Status.Should().Be(PreflightStatus.Fail);
        result.Message.Should().Contain("128000").And.Contain("agent.scout");
        result.FixHint.Should().Contain("max_context_tokens");
    }

    [Fact]
    public async Task Preflight_AThresholdWithNoStatedWindow_IsReported()
    {
        var result = await Run(window: null, maxContextTokens: 200000, ratio: 0.7);

        result.Status.Should().Be(PreflightStatus.Pass);
        result.Message.Should().Contain("no context_window_tokens stated")
            .And.Contain("agent.scout", "the operator is told WHICH roles derive nothing");
    }

    [Fact]
    public async Task Preflight_AThresholdBelowTheWindow_IsSilent()
    {
        var result = await Run(window: 128000, maxContextTokens: 100000, ratio: 0.7);

        result.Status.Should().Be(PreflightStatus.Pass);
        result.Message.Should().NotContain("agent.scout");
    }

    [Fact]
    public async Task Preflight_CompactionDisabled_IsSilent()
    {
        var result = await Run(window: 128000, maxContextTokens: 200000, ratio: 0.7, enabled: false);

        result.Status.Should().Be(PreflightStatus.Pass);
    }

    [Fact]
    public async Task Preflight_ConfigFailedToLoad_Skips()
    {
        var check = new ContextWindowThresholdCheck(
            FakePreflightConfigSource.LoadFailure("bad yaml"));

        (await check.RunAsync(CancellationToken.None)).Status.Should().Be(PreflightStatus.Skip);
    }

    private static async Task<PreflightCheckResult> Run(
        int? window, int maxContextTokens, double ratio, bool enabled = true)
    {
        var stated = new ModelAssignment { Model = "m", ContextWindowTokens = window };
        var config = new AgentSmithConfig
        {
            Agents = new Dictionary<string, AgentConfig>
            {
                ["agent"] = new()
                {
                    Type = "stub",
                    Models = new ModelRegistryConfig
                    {
                        Scout = stated, Primary = stated, Planning = stated,
                        Summarization = stated, ContextGeneration = stated,
                        CodeMapGeneration = stated,
                    },
                    Compaction = new CompactionConfig
                    {
                        IsEnabled = enabled,
                        MaxContextTokens = maxContextTokens,
                        MaxContextTokensTriggerRatio = ratio,
                    },
                },
            },
        };
        var check = new ContextWindowThresholdCheck(FakePreflightConfigSource.Of(config));
        return await check.RunAsync(CancellationToken.None);
    }
}
