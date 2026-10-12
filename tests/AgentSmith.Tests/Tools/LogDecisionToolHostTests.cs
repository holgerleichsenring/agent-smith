using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Tools;

public sealed class LogDecisionToolHostTests
{
    [Fact]
    public void GetTools_AnyPhase_ReturnsLogDecisionTool()
    {
        var host = new LogDecisionToolHost(Mock.Of<IDecisionLogger>(), repositories: null);

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
        logger.Setup(l => l.LogAsync(It.IsAny<ISandboxFileReader?>(), It.IsAny<DecisionCategory>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(DecisionLogOutcome.Recorded);
        var repository = Mock.Of<ISandboxFileReader>();
        var host = new LogDecisionToolHost(logger.Object, AgentSmith.Tests.TestHelpers.TestRepositories.One(repository));

        var result = await host.LogDecision("Architecture", "Chose composition over inheritance");

        result.Should().Contain("Architecture").And.Contain("Chose composition");
        logger.Verify(
            l => l.LogAsync(
                repository,
                DecisionCategory.Architecture,
                "Chose composition over inheritance",
                It.IsAny<CancellationToken>(),
                It.IsAny<string?>()),
            Times.Once,
            "the host hands on the REPOSITORY's file surface, not a path on this machine");
        host.GetDecisions().Should().ContainSingle()
            .Which.Category.Should().Be("Architecture");
    }

    [Fact]
    public async Task LogDecision_InvalidCategory_ReturnsStructuredError()
    {
        var host = new LogDecisionToolHost(Mock.Of<IDecisionLogger>(), repositories: null);

        var result = await host.LogDecision("not-a-category", "x");

        result.Should().StartWith("Error:").And.Contain("not-a-category");
        host.GetDecisions().Should().BeEmpty();
    }

    /// <summary>
    /// 2026-09-19-c511b: the decision IS recorded — the run's event stream took it — and the lost
    /// repository copy is not something the model can repair. The run that was told rewrote one
    /// decision four times in six seconds trying.
    /// </summary>
    [Fact]
    public async Task LogDecision_RepositoryWriteFailed_ReturnsTheSameSuccessTextAsASuccessfulWrite()
    {
        var written = await ResultFor(DecisionLogOutcome.Recorded);
        var lost = await ResultFor(DecisionLogOutcome.RecordedWithoutRepositoryCopy);

        lost.Should().Be(written);
    }

    [Fact]
    public async Task LogDecision_NothingRecorded_SaysSoInsteadOfReportingSuccess()
    {
        var host = HostReturning(DecisionLogOutcome.NotRecorded, out _);

        var result = await host.LogDecision("Architecture", "a choice");

        result.Should().StartWith("Decision NOT recorded").And.Contain("a choice");
        host.GetDecisions().Should().BeEmpty(
            "a decision that reached neither the run nor the repository is not one the host counts");
    }

    private static async Task<string> ResultFor(DecisionLogOutcome outcome) =>
        await HostReturning(outcome, out _).LogDecision("Architecture", "a choice");

    /// <summary>
    /// 2026-10-12-f5f7c: one host, a repository map — the file copy goes to the repository the
    /// call names, the run-wide list keeps every decision, and two repositories need a name.
    /// </summary>
    [Fact]
    public async Task LogDecisionToolHost_TwoRepos_FileCopyInNamedRepo()
    {
        var logger = new Mock<IDecisionLogger>();
        logger.Setup(l => l.LogAsync(It.IsAny<ISandboxFileReader?>(), It.IsAny<DecisionCategory>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(DecisionLogOutcome.Recorded);
        var server = Mock.Of<ISandboxFileReader>();
        var client = Mock.Of<ISandboxFileReader>();
        var host = new LogDecisionToolHost(logger.Object,
            new AgentSmith.Application.Services.Repositories.FixedRunRepositories(
                new Dictionary<string, ISandboxFileReader> { ["server"] = server, ["client"] = client }));

        var unnamed = await host.LogDecision("Architecture", "a choice");
        await host.LogDecision("Architecture", "server choice", "server");
        await host.LogDecision("Tooling", "client choice", "client");

        unnamed.Should().StartWith("Error:").And.Contain("server, client");
        logger.Verify(l => l.LogAsync(server, DecisionCategory.Architecture, "server choice",
            It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Once);
        logger.Verify(l => l.LogAsync(client, DecisionCategory.Tooling, "client choice",
            It.IsAny<CancellationToken>(), It.IsAny<string?>()), Times.Once);
        host.GetDecisions().Select(d => d.Decision).Should().Equal("server choice", "client choice");
    }

    private static LogDecisionToolHost HostReturning(
        DecisionLogOutcome outcome, out Mock<IDecisionLogger> logger)
    {
        logger = new Mock<IDecisionLogger>();
        logger.Setup(l => l.LogAsync(It.IsAny<ISandboxFileReader?>(), It.IsAny<DecisionCategory>(),
                It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(outcome);
        return new LogDecisionToolHost(logger.Object, repositories: null);
    }
}
