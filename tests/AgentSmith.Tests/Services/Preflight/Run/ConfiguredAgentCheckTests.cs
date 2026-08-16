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
