using AgentSmith.Application.Services.Polling;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Polling;

/// <summary>
/// p0139 fallback behavior: when a project's trigger-block omits
/// pipeline_from_label, the global pipeline_triggers map applies as a
/// default. A populated project-level map wins over the global default.
/// </summary>
public sealed class PipelineResolverFallbackTests
{
    private static readonly PipelineTriggerMap GlobalMap = new(
        new Dictionary<string, string>
        {
            ["bug"] = "fix-bug",
            ["feature"] = "add-feature",
        });

    [Fact]
    public void Resolve_ProjectMapNullAndLabelMatchesGlobal_ReturnsGlobalPipeline()
    {
        var trigger = new WebhookTriggerConfig { PipelineFromLabel = null };

        var result = PipelineResolver.Resolve(trigger, new[] { "bug" }, GlobalMap);

        result.Should().Be("fix-bug");
    }

    [Fact]
    public void Resolve_ProjectMapEmptyAndLabelMatchesGlobal_ReturnsGlobalPipeline()
    {
        var trigger = new WebhookTriggerConfig { PipelineFromLabel = new() };

        var result = PipelineResolver.Resolve(trigger, new[] { "feature" }, GlobalMap);

        result.Should().Be("add-feature");
    }

    [Fact]
    public void Resolve_ProjectMapPopulated_WinsOverGlobal()
    {
        var trigger = new WebhookTriggerConfig
        {
            PipelineFromLabel = new() { ["bug"] = "project-specific-pipeline" },
        };

        var result = PipelineResolver.Resolve(trigger, new[] { "bug" }, GlobalMap);

        result.Should().Be("project-specific-pipeline");
    }

    [Fact]
    public void Resolve_ProjectMapPopulatedNoMatch_DoesNotFallBackToGlobal()
    {
        var trigger = new WebhookTriggerConfig
        {
            PipelineFromLabel = new() { ["enhancement"] = "add-feature" },
        };

        var result = PipelineResolver.Resolve(trigger, new[] { "bug" }, GlobalMap);

        result.Should().BeNull();
    }

    [Fact]
    public void Resolve_NoGlobalAndProjectMapNull_ReturnsDefaultPipeline()
    {
        var trigger = new WebhookTriggerConfig
        {
            PipelineFromLabel = null,
            DefaultPipeline = "fallback-pipeline",
        };

        var result = PipelineResolver.Resolve(trigger, new[] { "any" }, globalTriggers: null);

        result.Should().Be("fallback-pipeline");
    }
}
