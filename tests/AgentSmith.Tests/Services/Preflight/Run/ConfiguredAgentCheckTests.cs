using AgentSmith.Application.Services.Preflight.Run;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Preflight;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Preflight.Run;

/// <summary>
/// p0428: the empty-placeholder config (p0419's structural defect) is named before the
/// run spends a phase diagnosing its consequences.
/// </summary>
public sealed class ConfiguredAgentCheckTests
{
    [Fact]
    public async Task AnEmptyConfig_FailsBeforeTheRunSpends()
    {
        var check = new ConfiguredAgentCheck(AgentSmithConfig.Empty());

        var finding = await check.RunAsync(PipelineWith(new AgentConfig()), CancellationToken.None);

        finding.Verdict.Should().Be(RunPreflightVerdict.Fail);
        finding.Message.Should().Contain("no agents are configured");
        finding.Lever.Should().Contain("--config");
    }

    [Fact]
    public async Task AConfiguredAgent_Passes()
    {
        var agent = new AgentConfig { Type = "claude", Model = "a-model" };
        var config = new AgentSmithConfig { Agents = { ["primary"] = agent } };

        var finding = await new ConfiguredAgentCheck(config)
            .RunAsync(PipelineWith(agent), CancellationToken.None);

        finding.Verdict.Should().Be(RunPreflightVerdict.Pass);
        finding.Message.Should().Contain("a-model");
    }

    /// <summary>
    /// p0436: the shape that was REFUSED in production — an azure_openai agent whose model
    /// comes from the per-role registry, which is how every agent in the operator's
    /// deployed config is written. The gate knew only the single-model shape and stopped a
    /// healthy run two seconds in, on the first real run after it shipped.
    /// </summary>
    [Fact]
    public async Task ConfiguredAgent_TheDeployedShapeThatWasRefused_Passes()
    {
        var agent = new AgentConfig
        {
            Type = "azure_openai",
            Models = new ModelRegistryConfig
            {
                Scout = new ModelAssignment { Model = "gpt-4.1-mini", MaxTokens = 8192 },
                Primary = new ModelAssignment { Model = "gpt-5.1", MaxTokens = 16384 },
            },
        };
        var config = new AgentSmithConfig { Agents = { ["azure-openai-default"] = agent } };

        var finding = await new ConfiguredAgentCheck(config)
            .RunAsync(PipelineWith(agent), CancellationToken.None);

        finding.Verdict.Should().Be(RunPreflightVerdict.Pass);
        finding.Message.Should().Contain("gpt-5.1", "the gate reports the model the runtime would resolve");
    }

    [Fact]
    public async Task ConfiguredAgent_WithNeitherShape_NamesBothPlacesAModelCanLive()
    {
        var agent = new AgentConfig { Type = "azure_openai" };
        var config = new AgentSmithConfig { Agents = { ["a"] = agent } };

        var finding = await new ConfiguredAgentCheck(config)
            .RunAsync(PipelineWith(agent), CancellationToken.None);

        finding.Verdict.Should().Be(RunPreflightVerdict.Fail);
        finding.Lever.Should().Contain(".model").And.Contain("models.primary.model",
            "someone reading this believes their config is right — name every place it could be");
    }

    [Fact]
    public async Task AnAgentWithoutAModel_NamesTheMissingField()
    {
        var agent = new AgentConfig { Type = "claude" };
        var config = new AgentSmithConfig { Agents = { ["primary"] = agent } };

        var finding = await new ConfiguredAgentCheck(config)
            .RunAsync(PipelineWith(agent), CancellationToken.None);

        finding.Verdict.Should().Be(RunPreflightVerdict.Fail);
        finding.Message.Should().Contain("its model");
        finding.Lever.Should().NotBeNullOrWhiteSpace();
    }

    private static PipelineContext PipelineWith(AgentConfig agent)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.ResolvedPipeline,
            new ResolvedPipelineConfig("code", agent, "skills", null));
        return pipeline;
    }
}
