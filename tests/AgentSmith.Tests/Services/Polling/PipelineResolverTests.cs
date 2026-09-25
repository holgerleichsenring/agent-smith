using AgentSmith.Application.Services.Polling;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Polling;

public sealed class PipelineResolverTests
{
    // 2026-09-16-a4d7: WebhookTriggerConfig.DefaultPipeline is nullable now, so a trigger
    // can state that it declares none. The behaviour for that trigger is UNCHANGED — the
    // literal moved out of the config object and into the one place that answers with it.
    [Fact]
    public void PipelineResolver_TriggerStatesNoDefault_AnswersTheCodePreset()
    {
        // 2026-09-25-e5b1: the answer was the alias `fix-bug`, which was the code preset under
        // a name that no longer resolves. The BEHAVIOUR is unchanged — the same pipeline runs.
        var trigger = new WebhookTriggerConfig();

        trigger.DefaultPipeline.Should().BeNull();
        new PipelineResolver().Resolve(trigger, ["some-label"]).Should().Be(PipelinePresets.CodeName);
        PipelinePresets.UndeclaredFallbackPipeline.Should().Be(PipelinePresets.CodeName);
    }

    [Fact]
    public void Resolve_EmptyPipelineFromLabel_ReturnsDefaultPipeline()
    {
        var trigger = new WebhookTriggerConfig { DefaultPipeline = "code" };

        var pipeline = new PipelineResolver().Resolve(trigger, ["bug"]);

        pipeline.Should().Be("code");
    }

    [Fact]
    public void Resolve_FirstMatchingLabelWinsInInsertionOrder()
    {
        var trigger = new WebhookTriggerConfig
        {
            DefaultPipeline = "code",
            PipelineFromLabel = new()
            {
                ["bug"] = "code",
                ["feature"] = "implement-feature",
                ["security-review"] = "security-scan"
            }
        };

        var pipeline = new PipelineResolver().Resolve(trigger, ["feature", "security-review"]);

        pipeline.Should().Be("implement-feature");
    }

    [Fact]
    public void Resolve_LabelMatchIsCaseInsensitive()
    {
        var trigger = new WebhookTriggerConfig
        {
            PipelineFromLabel = new() { ["Bug"] = "code" }
        };

        var pipeline = new PipelineResolver().Resolve(trigger, ["BUG"]);

        pipeline.Should().Be("code");
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
                ["bug"] = "code"
            }
        };

        var pipeline = new PipelineResolver().Resolve(trigger, ["agent-smith:pending"]);

        pipeline.Should().BeNull();
    }

    [Fact]
    public void Resolve_NoMatchAndPipelineFromLabelNonEmpty_ReturnsNull()
    {
        var trigger = new WebhookTriggerConfig
        {
            DefaultPipeline = "code",
            PipelineFromLabel = new() { ["security-review"] = "security-scan" }
        };

        var pipeline = new PipelineResolver().Resolve(trigger, ["bug"]);

        pipeline.Should().BeNull();
    }

    [Fact]
    public void Resolve_EmptyLabels_AndNonEmptyMap_ReturnsNull()
    {
        var trigger = new WebhookTriggerConfig
        {
            DefaultPipeline = "code",
            PipelineFromLabel = new() { ["bug"] = "code" }
        };

        var pipeline = new PipelineResolver().Resolve(trigger, []);

        pipeline.Should().BeNull();
    }

    [Fact]
    public void Resolve_EmptyLabels_AndEmptyMap_ReturnsDefaultPipeline()
    {
        var trigger = new WebhookTriggerConfig { DefaultPipeline = "code" };

        var pipeline = new PipelineResolver().Resolve(trigger, []);

        pipeline.Should().Be("code");
    }

    [Fact]
    public void Resolve_OperatorAgentSmithPrefixedLabel_PassesThroughFilter()
    {
        // p0133 follow-up: operator-defined labels that share the agent-smith:
        // prefix (init / bug / feature / scan etc.) must survive the lifecycle
        // filter — only the 5 closed-set status labels get stripped.
        var trigger = new WebhookTriggerConfig
        {
            DefaultPipeline = "code",
            PipelineFromLabel = new()
            {
                ["agent-smith:init"] = "init-project",
                ["agent-smith:bug"] = "code"
            }
        };

        var pipeline = new PipelineResolver().Resolve(trigger, ["agent-smith:init"]);

        pipeline.Should().Be("init-project");
    }

    [Fact]
    public void Resolve_LifecycleStatusAndOperatorLabel_OnlyOperatorMatchesAfterFilter()
    {
        // Mixed input — lifecycle status labels (agent-smith:enqueued etc.) get
        // filtered out, operator-defined ones pass through.
        var trigger = new WebhookTriggerConfig
        {
            DefaultPipeline = "code",
            PipelineFromLabel = new()
            {
                ["agent-smith:init"] = "init-project"
            }
        };

        var pipeline = new PipelineResolver().Resolve(
            trigger,
            ["agent-smith:enqueued", "agent-smith:init"]);

        pipeline.Should().Be("init-project");
    }
}
