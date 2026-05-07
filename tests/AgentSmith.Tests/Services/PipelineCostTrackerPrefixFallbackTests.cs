using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Services;

public sealed class PipelineCostTrackerPrefixFallbackTests
{
    [Fact]
    public void EstimateCostUsd_DateSuffixedAzureModel_UsesBaseModelPricing()
    {
        var tracker = new PipelineCostTracker(new PricingConfig
        {
            Models = new() { ["gpt-4.1"] = new() { InputPerMillion = 2.0m, OutputPerMillion = 8.0m } }
        });
        tracker.Track(BuildResponse("gpt-4.1-2025-04-14", input: 1_000_000, output: 1_000_000));

        tracker.EstimateCostUsd().Should().Be(10.0m);
    }

    [Fact]
    public void EstimateCostUsd_DateSuffixedClaude_UsesBaseModelPricing()
    {
        var tracker = new PipelineCostTracker(new PricingConfig
        {
            Models = new() { ["claude-sonnet-4-5"] = new() { InputPerMillion = 3.0m, OutputPerMillion = 15.0m } }
        });
        tracker.Track(BuildResponse("claude-sonnet-4-5-20250929", input: 1_000_000, output: 0));

        tracker.EstimateCostUsd().Should().Be(3.0m);
    }

    [Fact]
    public void EstimateCostUsd_UnknownModelNoPrefix_ReturnsZero()
    {
        var tracker = new PipelineCostTracker(new PricingConfig
        {
            Models = new() { ["gpt-4.1"] = new() { InputPerMillion = 2.0m, OutputPerMillion = 8.0m } }
        });
        tracker.Track(BuildResponse("some-other-model-xyz", input: 1_000_000, output: 1_000_000));

        tracker.EstimateCostUsd().Should().Be(0m);
    }

    [Fact]
    public void EstimateCostUsd_LongestPrefixWins()
    {
        var tracker = new PipelineCostTracker(new PricingConfig
        {
            Models = new()
            {
                ["gpt-4.1"] = new() { InputPerMillion = 2.0m, OutputPerMillion = 8.0m },
                ["gpt-4.1-mini"] = new() { InputPerMillion = 0.40m, OutputPerMillion = 1.60m }
            }
        });
        tracker.Track(BuildResponse("gpt-4.1-mini-2024-07-18", input: 1_000_000, output: 0));

        tracker.EstimateCostUsd().Should().Be(0.40m);
    }

    private static ChatResponse BuildResponse(string modelId, int input, int output) => new()
    {
        ModelId = modelId,
        Usage = new UsageDetails
        {
            InputTokenCount = input,
            OutputTokenCount = output
        }
    };
}
