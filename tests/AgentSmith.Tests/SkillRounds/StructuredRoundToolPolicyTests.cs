using AgentSmith.Application.Services.SkillRounds;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SkillRounds;

public sealed class StructuredRoundToolPolicyTests
{
    private static StructuredRoundToolPolicy BuildPolicy()
    {
        var pipelinePolicy = new Mock<IPipelineToolPolicy>();
        pipelinePolicy.Setup(p => p.GetAllowedHosts(It.IsAny<string>()))
            .Returns(new HashSet<Type>
            {
                typeof(FilesystemToolHost),
                typeof(LogDecisionToolHost)
            });
        var toolKit = new ToolKit(pipelinePolicy.Object);
        return new StructuredRoundToolPolicy(
            toolKit,
            new Mock<IDecisionLogger>().Object,
            NullLogger<StructuredRoundToolPolicy>.Instance,
            NullLoggerFactory.Instance);
    }

    private static PipelineContext PipelineFor(string pipelineName, bool activeMode = false)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandbox, new Mock<ISandbox>().Object);
        pipeline.Set(ContextKeys.PipelineName, pipelineName);
        pipeline.Set(ContextKeys.ActiveMode, activeMode);
        return pipeline;
    }

    [Fact]
    public void StructuredRoundToolPolicy_ApiSecurityActiveMode_IncludesProbe()
    {
        var policy = BuildPolicy();
        var role = new RoleSkillDefinition { Name = "auth-skill", Role = "investigator" };
        var pipeline = PipelineFor("api-security-scan", activeMode: true);

        var tools = policy.GetTools(role, pipeline);

        // Active mode probing flows through the prompt's JSON contract (no
        // separate IToolHost wraps it today); the tool set always carries
        // Read/Grep so the LLM can verify probe targets against the spec.
        ToolNames(tools).Should().Contain(new[] { "read_file", "grep" });
    }

    [Fact]
    public void StructuredRoundToolPolicy_ApiSecurityPassiveMode_KeepsToolsuiteIntact()
    {
        var policy = BuildPolicy();
        var role = new RoleSkillDefinition { Name = "auth-skill", Role = "investigator" };
        var pipeline = PipelineFor("api-security-scan", activeMode: false);

        var tools = policy.GetTools(role, pipeline);

        // Active vs passive is a prompt-layer distinction (the probe JSON
        // block) — not a tool-layer one. p0151a: Plan-phase tools include
        // RunCommand for recon regardless of active/passive mode.
        ToolNames(tools).Should().Contain(new[] { "read_file", "grep", "run_command" });
    }

    [Fact]
    public void StructuredRoundToolPolicy_SecurityScanTriage_ReturnsEmpty()
    {
        var policy = BuildPolicy();
        var role = new RoleSkillDefinition { Name = "triage", Role = "filter" };
        var pipeline = PipelineFor("security-scan");

        var tools = policy.GetTools(role, pipeline);

        tools.Should().BeEmpty();
    }

    private static IEnumerable<string> ToolNames(IEnumerable<AITool> tools) =>
        tools.OfType<AIFunction>().Select(f => f.Name);
}
