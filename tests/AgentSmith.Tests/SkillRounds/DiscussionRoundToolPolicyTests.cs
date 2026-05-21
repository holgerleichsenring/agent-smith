using AgentSmith.Application.Services.SkillRounds;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Moq;

namespace AgentSmith.Tests.SkillRounds;

public sealed class DiscussionRoundToolPolicyTests
{
    private static DiscussionRoundToolPolicy BuildPolicy()
    {
        // Allow all hosts so the policy's tool selection is what we measure.
        var pipelinePolicy = new Mock<IPipelineToolPolicy>();
        pipelinePolicy.Setup(p => p.GetAllowedHosts(It.IsAny<string>()))
            .Returns(new HashSet<Type>
            {
                typeof(FilesystemToolHost),
                typeof(LogDecisionToolHost),
                typeof(HumanToolHost)
            });
        var toolKit = new ToolKit(pipelinePolicy.Object);
        var decisionLogger = new Mock<IDecisionLogger>().Object;
        return new DiscussionRoundToolPolicy(toolKit, decisionLogger);
    }

    private static PipelineContext PipelineWithSandbox(string pipelineName = "fix-bug")
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandbox, new Mock<ISandbox>().Object);
        pipeline.Set(ContextKeys.PipelineName, pipelineName);
        return pipeline;
    }

    [Fact]
    public void DiscussionRoundToolPolicy_VerifyHintMode_ReturnsReadAndGrep()
    {
        var policy = BuildPolicy();
        var role = new RoleSkillDefinition { Name = "x", Role = "investigator", InvestigatorMode = "verify_hint" };
        var pipeline = PipelineWithSandbox();

        var tools = policy.GetTools(role, pipeline);

        tools.Should().NotBeEmpty();
        ToolNames(tools).Should().Contain(new[] { "read_file", "grep_in_tree", "list_directory" });
    }

    [Fact]
    public void DiscussionRoundToolPolicy_SurveyMode_ReturnsFullToolSet()
    {
        var policy = BuildPolicy();
        var role = new RoleSkillDefinition { Name = "x", Role = "investigator", InvestigatorMode = "survey" };
        var pipeline = PipelineWithSandbox();

        var tools = policy.GetTools(role, pipeline);

        ToolNames(tools).Should().Contain(new[] { "read_file", "grep_in_tree", "list_directory" });
    }

    [Fact]
    public void DiscussionRoundToolPolicy_PureProseDiscussion_ReturnsEmpty()
    {
        var policy = BuildPolicy();
        var role = new RoleSkillDefinition { Name = "x", Role = "producer", InvestigatorMode = null };
        var pipeline = PipelineWithSandbox("mad-discussion");

        var tools = policy.GetTools(role, pipeline);

        tools.Should().BeEmpty();
    }

    [Fact]
    public void DiscussionRoundToolPolicy_NoSandbox_ReturnsEmpty()
    {
        var policy = BuildPolicy();
        var role = new RoleSkillDefinition { Name = "x", Role = "investigator", InvestigatorMode = "verify_hint" };
        var pipeline = new PipelineContext();

        var tools = policy.GetTools(role, pipeline);

        tools.Should().BeEmpty();
    }

    private static IEnumerable<string> ToolNames(IEnumerable<AITool> tools) =>
        tools.OfType<AIFunction>().Select(f => f.Name);
}
