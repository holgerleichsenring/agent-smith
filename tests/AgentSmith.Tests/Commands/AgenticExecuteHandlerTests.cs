using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

public sealed class AgenticExecuteHandlerTests
{
    private readonly Mock<IAgentProviderFactory> _factoryMock = new();
    private readonly Mock<IProgressReporter> _reporterMock = new();
    private readonly AgenticExecuteHandler _handler;

    public AgenticExecuteHandlerTests()
    {
        _handler = new AgenticExecuteHandler(
            _factoryMock.Object,
            _reporterMock.Object,
            NullLoggerFactory.Instance.CreateLogger<AgenticExecuteHandler>());
    }

    [Fact]
    public async Task ExecuteAsync_Success_StoresCodeChangesInPipeline()
    {
        var changes = new List<CodeChange>
        {
            new(new FilePath("file.cs"), "content", "Create")
        };
        var providerMock = new Mock<IAgentProvider>();
        providerMock.Setup(p => p.ExecutePlanAsync(
                It.IsAny<Plan>(), It.IsAny<Repository>(),
                It.IsAny<string>(), It.IsAny<IProgressReporter?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(changes);
        _factoryMock.Setup(f => f.Create(It.IsAny<AgentConfig>()))
            .Returns(providerMock.Object);

        var plan = new Plan("Test", new List<PlanStep>(), "{}");
        var repo = new Repository("/tmp", new BranchName("main"), "https://github.com/org/repo.git");
        var pipeline = new PipelineContext();
        var context = new AgenticExecuteContext(
            plan, repo, "principles", new AgentConfig { Type = "claude" }, pipeline);

        var result = await _handler.ExecuteAsync(context);

        result.IsSuccess.Should().BeTrue();
        pipeline.Get<IReadOnlyList<CodeChange>>(ContextKeys.CodeChanges).Should().HaveCount(1);
    }

    [Fact]
    public async Task ExecuteAsync_ProviderThrows_PropagatesException()
    {
        var providerMock = new Mock<IAgentProvider>();
        providerMock.Setup(p => p.ExecutePlanAsync(
                It.IsAny<Plan>(), It.IsAny<Repository>(),
                It.IsAny<string>(), It.IsAny<IProgressReporter?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Execution failed"));
        _factoryMock.Setup(f => f.Create(It.IsAny<AgentConfig>()))
            .Returns(providerMock.Object);

        var plan = new Plan("Test", new List<PlanStep>(), "{}");
        var repo = new Repository("/tmp", new BranchName("main"), "https://github.com/org/repo.git");
        var pipeline = new PipelineContext();
        var context = new AgenticExecuteContext(
            plan, repo, "principles", new AgentConfig { Type = "claude" }, pipeline);

        var act = async () => await _handler.ExecuteAsync(context);

        await act.Should().ThrowAsync<Exception>().WithMessage("Execution failed");
    }
}
