using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Builders;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// Covers PipelineExecutor's read-only-pipeline guard around the WIP-persist wrapper:
/// scan pipelines (security-scan, api-security-scan) — which have no
/// AgenticExecute/GenerateTests/GenerateDocs handlers — must not trigger
/// PersistWorkBranch on failure even when a Repository is in context.
/// Without the guard, scan-pipeline failures used to attempt to stage scan
/// artifacts (ZAP reports, findings JSON) into a WIP branch.
/// </summary>
public sealed class PipelineExecutorPersistGuardTests
{
    private readonly Mock<ICommandExecutor> _executorMock = new();
    private readonly Mock<ICommandContextFactory> _factoryMock = new();
    private readonly Mock<ITicketProviderFactory> _ticketFactoryMock = new();
    private readonly Mock<IPipelineLifecycleCoordinator> _lifecycleMock = new();
    private readonly Mock<IProgressReporter> _progressReporterMock = new();
    private readonly Mock<ISandboxFactory> _sandBoxFactory = new();
    private readonly PipelineExecutor _sut;

    public PipelineExecutorPersistGuardTests()
    {
        _lifecycleMock
            .Setup(c => c.BeginAsync(It.IsAny<ProjectConfig>(), It.IsAny<PipelineContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Mock.Of<IAsyncPipelineLifecycle>());
        _sut = new PipelineExecutor(
            _executorMock.Object,
            _factoryMock.Object,
            _ticketFactoryMock.Object,
            _lifecycleMock.Object,
            _sandBoxFactory.Object,
            new SandboxSpecBuilder(), 
            _progressReporterMock.Object, 
            NullLogger<PipelineExecutor>.Instance 
        );
    }

    [Fact]
    public async Task ExecuteAsync_ScanPipelineFails_DoesNotCallPersistWorkBranchEvenWithRepository()
    {
        var pipeline = NewPipelineWithRepository();
        var commands = new[] { CommandNames.SpawnNuclei, CommandNames.Triage, CommandNames.DeliverFindings };
        ArrangeFirstCommandFailure(commands[0]);

        var result = await _sut.ExecuteAsync(commands, new ProjectConfig(), pipeline, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        AssertPersistWasNotInvoked();
    }

    [Fact]
    public async Task ExecuteAsync_SourcelessPipelineFails_DoesNotCallPersistWorkBranch()
    {
        var pipeline = new PipelineContext();
        var commands = new[] { CommandNames.AgenticExecute };
        ArrangeFirstCommandFailure(commands[0]);

        var result = await _sut.ExecuteAsync(commands, NewProjectConfigWithImage(), pipeline, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        AssertPersistWasNotInvoked();
    }

    [Fact]
    public async Task ExecuteAsync_CodeModifyingPipelineFails_AttemptsPersistWorkBranch()
    {
        var pipeline = NewPipelineWithRepository();
        var commands = new[] { CommandNames.AgenticExecute, CommandNames.Test };
        ArrangeFirstCommandFailure(commands[0]);

        var result = await _sut.ExecuteAsync(commands, NewProjectConfigWithImage(), pipeline, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        _factoryMock.Verify(f => f.Create(
            It.Is<PipelineCommand>(c => c.Name == CommandNames.PersistWorkBranch),
            It.IsAny<ProjectConfig>(),
            It.IsAny<PipelineContext>()),
            Times.Once);
    }

    static ProjectConfig NewProjectConfigWithImage()
    {
        return new ProjectConfig
        {
            Sandbox = new SandboxConfig
            {
                ToolchainImage = "dotnet8"
            },
        };
    }

    private static PipelineContext NewPipelineWithRepository()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Repository,
            new Repository("/tmp/some/path", new BranchName("main"), "https://example.com/repo.git"));
        return pipeline;
    }

    private void ArrangeFirstCommandFailure(string commandName)
    {
        _factoryMock.Setup(f => f.Create(
            It.Is<PipelineCommand>(c => c.Name == commandName),
            It.IsAny<ProjectConfig>(),
            It.IsAny<PipelineContext>()))
            .Throws(new Exception($"{commandName} crashed for test"));
    }

    private void AssertPersistWasNotInvoked()
    {
        _factoryMock.Verify(f => f.Create(
            It.Is<PipelineCommand>(c => c.Name == CommandNames.PersistWorkBranch),
            It.IsAny<ProjectConfig>(),
            It.IsAny<PipelineContext>()),
            Times.Never);
    }
}
