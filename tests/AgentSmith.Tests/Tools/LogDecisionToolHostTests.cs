using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Decisions;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Tools;

public sealed class LogDecisionToolHostTests
{
    [Fact]
    public void GetTools_AnyPhase_ReturnsLogDecisionTool()
    {
        var host = new LogDecisionToolHost(Mock.Of<IDecisionLogger>());

        foreach (var phase in Enum.GetValues<SkillExecutionPhase>())
        {
            var names = host.GetTools(phase, null).Select(f => f.Name).ToList();
            names.Should().BeEquivalentTo(new[] { "log_decision" }, $"phase {phase} should expose only log_decision");
        }
    }

    [Fact]
    public async Task LogDecision_DelegatesToDecisionLogger()
    {
        var logger = new Mock<IDecisionLogger>();
        var host = new LogDecisionToolHost(logger.Object, repoPath: "/repo");

        var result = await host.LogDecision("Architecture", "Chose composition over inheritance");

        result.Should().Contain("Architecture").And.Contain("Chose composition");
        logger.Verify(
            l => l.LogAsync(
                "/repo",
                DecisionCategory.Architecture,
                "Chose composition over inheritance",
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>()),
            Times.Once);
        host.GetDecisions().Should().ContainSingle()
            .Which.Category.Should().Be("Architecture");
    }

    [Fact]
    public async Task LogDecision_InvalidCategory_ReturnsStructuredError()
    {
        var host = new LogDecisionToolHost(Mock.Of<IDecisionLogger>());

        var result = await host.LogDecision("not-a-category", "x");

        result.Should().StartWith("Error:").And.Contain("not-a-category");
        host.GetDecisions().Should().BeEmpty();
    }
}
