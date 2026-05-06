using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Lifecycle;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using AgentSmith.Sandbox.Wire;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

public sealed class PersistWorkBranchHandlerTests
{
    private readonly Mock<ISandbox> _sandboxMock = new();
    private readonly PersistWorkBranchHandler _handler;
    private readonly List<Step> _capturedSteps = new();

    public PersistWorkBranchHandlerTests()
    {
        _sandboxMock.Setup(s => s.RunStepAsync(
                It.IsAny<Step>(), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
            {
                _capturedSteps.Add(step);
                return Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 0, false, 0.1, null));
            });
        _handler = new PersistWorkBranchHandler(
            new SandboxGitOperations(NullLogger<SandboxGitOperations>.Instance),
            NullLogger<PersistWorkBranchHandler>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_NoRepositoryInPipeline_FailsWithUnknownKind()
    {
        var pipeline = NewPipelineWithSandbox();
        var ctx = NewContext(pipeline);

        var result = await _handler.ExecuteAsync(ctx, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        pipeline.Get<PersistFailureKind>(ContextKeys.PersistFailureKind)
            .Should().Be(PersistFailureKind.Unknown);
        _capturedSteps.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_NoSandboxInPipeline_FailsWithUnknownKind()
    {
        var pipeline = new PipelineContext();
        var repo = new Repository("/work", new BranchName("agent-smith/18693"), "https://x/y.git");
        pipeline.Set(ContextKeys.Repository, repo);

        var result = await _handler.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        pipeline.Get<PersistFailureKind>(ContextKeys.PersistFailureKind)
            .Should().Be(PersistFailureKind.Unknown);
    }

    [Fact]
    public async Task ExecuteAsync_CommitAndPushSucceeds_ReturnsOkAndDoesNotSetFailureKind()
    {
        var pipeline = NewPipelineWithRepoAndSandbox();
        var ctx = NewContext(pipeline);

        var result = await _handler.ExecuteAsync(ctx, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<PersistFailureKind>(ContextKeys.PersistFailureKind, out _).Should().BeFalse();
        _capturedSteps.Should().Contain(s => s.Command == "git" && s.Args!.Contains("commit"));
        _capturedSteps.Should().Contain(s => s.Command == "git" && s.Args!.Contains("push"));
    }

    [Fact]
    public async Task ExecuteAsync_CommitMessageIncludesPipelineContextValues()
    {
        var pipeline = NewPipelineWithRepoAndSandbox();
        pipeline.Set(ContextKeys.RunId, "abc12345");
        pipeline.Set(ContextKeys.FailedStepName, "Generating plan");
        pipeline.Set(ContextKeys.ResolvedPipeline,
            new ResolvedPipelineConfig("default", new AgentConfig(), "/skills", null));

        await _handler.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        var commitStep = _capturedSteps.FirstOrDefault(s => s.Command == "git" && s.Args!.Contains("commit"));
        commitStep.Should().NotBeNull();
        var message = commitStep!.Args!.Last();
        message.Should().Contain("Run-Id: abc12345");
        message.Should().Contain("Pipeline: default");
        message.Should().Contain("Failed-Step: Generating plan");
    }

    [Fact]
    public async Task ExecuteAsync_NothingToCommit_FailsWithNoChangesKind()
    {
        var pipeline = NewPipelineWithRepoAndSandbox();
        SetupCommitFailure("nothing to commit, working tree clean");

        var result = await _handler.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        pipeline.Get<PersistFailureKind>(ContextKeys.PersistFailureKind)
            .Should().Be(PersistFailureKind.NoChanges);
    }

    [Fact]
    public async Task ExecuteAsync_AuthFailureOnPush_FailsWithAuthDeniedKind()
    {
        var pipeline = NewPipelineWithRepoAndSandbox();
        SetupPushFailure("HTTP 401 Unauthorized");

        var result = await _handler.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        pipeline.Get<PersistFailureKind>(ContextKeys.PersistFailureKind)
            .Should().Be(PersistFailureKind.AuthDenied);
    }

    [Fact]
    public async Task ExecuteAsync_DivergentRemoteOnPush_FailsWithRemoteDivergentKind()
    {
        var pipeline = NewPipelineWithRepoAndSandbox();
        SetupPushFailure("push rejected: non-fast-forward update");

        var result = await _handler.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        pipeline.Get<PersistFailureKind>(ContextKeys.PersistFailureKind)
            .Should().Be(PersistFailureKind.RemoteDivergent);
    }

    [Fact]
    public async Task ExecuteAsync_UnclassifiedException_FailsWithUnknownKind()
    {
        var pipeline = NewPipelineWithRepoAndSandbox();
        SetupPushFailure("disk full");

        var result = await _handler.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        pipeline.Get<PersistFailureKind>(ContextKeys.PersistFailureKind)
            .Should().Be(PersistFailureKind.Unknown);
    }

    private void SetupCommitFailure(string errorMessage)
    {
        _sandboxMock.Setup(s => s.RunStepAsync(
                It.Is<Step>(st => st.Args!.Contains("commit")), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 1, false, 0.1, errorMessage)));
    }

    private void SetupPushFailure(string errorMessage)
    {
        _sandboxMock.Setup(s => s.RunStepAsync(
                It.Is<Step>(st => st.Args!.Contains("push")), It.IsAny<IProgress<StepEvent>?>(), It.IsAny<CancellationToken>()))
            .Returns<Step, IProgress<StepEvent>?, CancellationToken>((step, _, _) =>
                Task.FromResult(new StepResult(StepResult.CurrentSchemaVersion, step.StepId, 128, false, 0.1, errorMessage)));
    }

    private PipelineContext NewPipelineWithSandbox()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Sandbox, _sandboxMock.Object);
        return pipeline;
    }

    private PipelineContext NewPipelineWithRepoAndSandbox()
    {
        var pipeline = NewPipelineWithSandbox();
        var repo = new Repository(
            "/work",
            new BranchName("agent-smith/18693"),
            "https://example.com/x/y.git");
        pipeline.Set(ContextKeys.Repository, repo);
        return pipeline;
    }

    private static PersistWorkBranchContext NewContext(PipelineContext pipeline) =>
        new(new SourceConfig { Type = "azurerepos", Url = "https://dev.azure.com/x/y" },
            new AgentConfig(),
            pipeline);
}
