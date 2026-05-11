using AgentSmith.Application.Services.Polling;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Polling;

public sealed class PipelineResolverTests
{
    [Fact]
    public void Resolve_EmptyPipelineFromLabel_ReturnsDefaultPipeline()
    {
        var trigger = new WebhookTriggerConfig { DefaultPipeline = "fix-bug" };

        var pipeline = PipelineResolver.Resolve(trigger, ["bug"]);

        pipeline.Should().Be("fix-bug");
    }

    [Fact]
    public void Resolve_FirstMatchingLabelWinsInInsertionOrder()
    {
        var trigger = new WebhookTriggerConfig
        {
            DefaultPipeline = "fix-bug",
            PipelineFromLabel = new()
            {
                ["bug"] = "fix-bug",
                ["feature"] = "implement-feature",
                ["security-review"] = "security-scan"
            }
        };

        var pipeline = PipelineResolver.Resolve(trigger, ["feature", "security-review"]);

        pipeline.Should().Be("implement-feature");
    }

    [Fact]
    public void Resolve_LabelMatchIsCaseInsensitive()
    {
        var trigger = new WebhookTriggerConfig
        {
            PipelineFromLabel = new() { ["Bug"] = "fix-bug" }
        };

        var pipeline = PipelineResolver.Resolve(trigger, ["BUG"]);

        pipeline.Should().Be("fix-bug");
    }

    [Fact]
    public void Resolve_LifecycleLabelInInput_DoesNotMatchPipelineFromLabelKey()
    {
        var trigger = new WebhookTriggerConfig
        {
            DefaultPipeline = "default-pipeline",
            PipelineFromLabel = new()
            {
                ["agent-smith:pending"] = "trapped-pipeline",
                ["bug"] = "fix-bug"
            }
        };

        var pipeline = PipelineResolver.Resolve(trigger, ["agent-smith:pending"]);

        pipeline.Should().BeNull();
    }

    [Fact]
    public void Resolve_NoMatchAndPipelineFromLabelNonEmpty_ReturnsNull()
    {
        var trigger = new WebhookTriggerConfig
        {
            DefaultPipeline = "fix-bug",
            PipelineFromLabel = new() { ["security-review"] = "security-scan" }
        };

        var pipeline = PipelineResolver.Resolve(trigger, ["bug"]);

        pipeline.Should().BeNull();
    }

    [Fact]
    public void Resolve_EmptyLabels_AndNonEmptyMap_ReturnsNull()
    {
        var trigger = new WebhookTriggerConfig
        {
            DefaultPipeline = "fix-bug",
            PipelineFromLabel = new() { ["bug"] = "fix-bug" }
        };

        var pipeline = PipelineResolver.Resolve(trigger, []);

        pipeline.Should().BeNull();
    }

    [Fact]
    public void Resolve_EmptyLabels_AndEmptyMap_ReturnsDefaultPipeline()
    {
        var trigger = new WebhookTriggerConfig { DefaultPipeline = "fix-bug" };

        var pipeline = PipelineResolver.Resolve(trigger, []);

        pipeline.Should().Be("fix-bug");
    }

    [Fact]
    public void Resolve_OperatorAgentSmithPrefixedLabel_PassesThroughFilter()
    {
        // p0133 follow-up: operator-defined labels that share the agent-smith:
        // prefix (init / bug / feature / scan etc.) must survive the lifecycle
        // filter — only the 5 closed-set status labels get stripped.
        var trigger = new WebhookTriggerConfig
        {
            DefaultPipeline = "fix-bug",
            PipelineFromLabel = new()
            {
                ["agent-smith:init"] = "init-project",
                ["agent-smith:bug"] = "fix-bug"
            }
        };

        var pipeline = PipelineResolver.Resolve(trigger, ["agent-smith:init"]);

        pipeline.Should().Be("init-project");
    }

    [Fact]
    public void Resolve_LifecycleStatusAndOperatorLabel_OnlyOperatorMatchesAfterFilter()
    {
        // Mixed input — lifecycle status labels (agent-smith:enqueued etc.) get
        // filtered out, operator-defined ones pass through.
        var trigger = new WebhookTriggerConfig
        {
            DefaultPipeline = "fix-bug",
            PipelineFromLabel = new()
            {
                ["agent-smith:init"] = "init-project"
            }
        };

        var pipeline = PipelineResolver.Resolve(
            trigger,
            ["agent-smith:enqueued", "agent-smith:init"]);

        pipeline.Should().Be("init-project");
    }
}
