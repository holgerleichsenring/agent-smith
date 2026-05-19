using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// Covers PipelineExecutor's read-only-pipeline guard around the WIP-persist wrapper:
/// scan pipelines (security-scan, api-security-scan) — which have no
/// AgenticExecute/GenerateTests/GenerateDocs handlers — must not trigger
/// PersistWorkBranch on failure even when a Repository is in context.
/// Without the guard, scan-pipeline failures used to attempt to stage scan
/// artifacts (ZAP reports, findings JSON) into a WIP branch.
///
/// Parametrised across the new composed executor and the pre-p0147e monolith.
/// </summary>
public sealed class PipelineExecutorPersistGuardTests
{
    public static IEnumerable<object[]> ExecutorShapes() => PipelineExecutorTestHarness.ExecutorShapes();

    [Theory]
    [MemberData(nameof(ExecutorShapes))]
    public async Task ExecuteAsync_ScanPipelineFails_DoesNotCallPersistWorkBranchEvenWithRepository(
        PipelineExecutorTestHarness.Shape shape)
    {
        var h = new PipelineExecutorTestHarness(shape);
        var pipeline = NewPipelineWithRepository();
        var commands = new[] { CommandNames.SpawnNuclei, CommandNames.Triage, CommandNames.DeliverFindings };
        ArrangeFirstCommandFailure(h, commands[0]);

        var result = await h.Sut.ExecuteAsync(commands, new ResolvedProject(), pipeline, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        AssertPersistWasNotInvoked(h);
    }

    [Theory]
    [MemberData(nameof(ExecutorShapes))]
    public async Task ExecuteAsync_SourcelessPipelineFails_DoesNotCallPersistWorkBranch(
        PipelineExecutorTestHarness.Shape shape)
    {
        var h = new PipelineExecutorTestHarness(shape);
        var pipeline = new PipelineContext();
        var commands = new[] { CommandNames.AgenticExecute };
        ArrangeFirstCommandFailure(h, commands[0]);

        var result = await h.Sut.ExecuteAsync(commands, NewProjectConfigWithImage(), pipeline, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        AssertPersistWasNotInvoked(h);
    }

    [Theory]
    [MemberData(nameof(ExecutorShapes))]
    public async Task ExecuteAsync_CodeModifyingPipelineFails_AttemptsPersistWorkBranch(
        PipelineExecutorTestHarness.Shape shape)
    {
        var h = new PipelineExecutorTestHarness(shape);
        var pipeline = NewPipelineWithRepository();
        var commands = new[] { CommandNames.AgenticExecute, CommandNames.Test };
        ArrangeFirstCommandFailure(h, commands[0]);

        var result = await h.Sut.ExecuteAsync(commands, NewProjectConfigWithImage(), pipeline, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        h.FactoryMock.Verify(f => f.Create(
            It.Is<PipelineCommand>(c => c.Name == CommandNames.PersistWorkBranch),
            It.IsAny<ResolvedProject>(),
            It.IsAny<PipelineContext>()),
            Times.Once);
    }

    private static ResolvedProject NewProjectConfigWithImage()
    {
        return new ResolvedProject
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
            new Repository(new BranchName("main"), "https://example.com/repo.git"));
        return pipeline;
    }

    private static void ArrangeFirstCommandFailure(PipelineExecutorTestHarness h, string commandName)
    {
        h.FactoryMock.Setup(f => f.Create(
            It.Is<PipelineCommand>(c => c.Name == commandName),
            It.IsAny<ResolvedProject>(),
            It.IsAny<PipelineContext>()))
            .Throws(new Exception($"{commandName} crashed for test"));
    }

    private static void AssertPersistWasNotInvoked(PipelineExecutorTestHarness h)
    {
        h.FactoryMock.Verify(f => f.Create(
            It.Is<PipelineCommand>(c => c.Name == CommandNames.PersistWorkBranch),
            It.IsAny<ResolvedProject>(),
            It.IsAny<PipelineContext>()),
            Times.Never);
    }
}
