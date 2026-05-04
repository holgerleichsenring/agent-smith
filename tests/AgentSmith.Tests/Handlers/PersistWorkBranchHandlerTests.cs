using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Lifecycle;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers;

public sealed class PersistWorkBranchHandlerTests
{
    private readonly Mock<ISourceProviderFactory> _factoryMock = new();
    private readonly Mock<ISourceProvider> _providerMock = new();
    private readonly PersistWorkBranchHandler _handler;

    public PersistWorkBranchHandlerTests()
    {
        _factoryMock.Setup(f => f.Create(It.IsAny<SourceConfig>())).Returns(_providerMock.Object);
        _handler = new PersistWorkBranchHandler(
            _factoryMock.Object,
            NullLogger<PersistWorkBranchHandler>.Instance);
    }

    [Fact]
    public async Task ExecuteAsync_NoRepositoryInPipeline_FailsWithUnknownKind()
    {
        var pipeline = new PipelineContext();
        var ctx = NewContext(pipeline);

        var result = await _handler.ExecuteAsync(ctx, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        pipeline.Get<PersistFailureKind>(ContextKeys.PersistFailureKind)
            .Should().Be(PersistFailureKind.Unknown);
        _providerMock.Verify(p => p.CommitAndPushAsync(
            It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_CommitAndPushSucceeds_ReturnsOkAndDoesNotSetFailureKind()
    {
        var pipeline = NewPipelineWithRepo();
        var ctx = NewContext(pipeline);

        var result = await _handler.ExecuteAsync(ctx, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        pipeline.TryGet<PersistFailureKind>(ContextKeys.PersistFailureKind, out _).Should().BeFalse();
        _providerMock.Verify(p => p.CommitAndPushAsync(
            It.IsAny<Repository>(),
            It.Is<string>(m => m.Contains("Run-Id:") && m.Contains("Pipeline:") && m.Contains("Failed-Step:")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_CommitMessageIncludesPipelineContextValues()
    {
        var pipeline = NewPipelineWithRepo();
        pipeline.Set(ContextKeys.RunId, "abc12345");
        pipeline.Set(ContextKeys.FailedStepName, "Generating plan");
        pipeline.Set(ContextKeys.ResolvedPipeline,
            new ResolvedPipelineConfig("default", new AgentConfig(), "/skills", null));
        var ctx = NewContext(pipeline);
        string? capturedMessage = null;
        _providerMock.Setup(p => p.CommitAndPushAsync(
                It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<Repository, string, CancellationToken>((_, msg, _) => capturedMessage = msg)
            .Returns(Task.CompletedTask);

        await _handler.ExecuteAsync(ctx, CancellationToken.None);

        capturedMessage.Should().NotBeNull();
        capturedMessage!.Should().Contain("Run-Id: abc12345");
        capturedMessage.Should().Contain("Pipeline: default");
        capturedMessage.Should().Contain("Failed-Step: Generating plan");
    }

    [Fact]
    public async Task ExecuteAsync_EmptyCommitException_FailsWithNoChangesKind()
    {
        var pipeline = NewPipelineWithRepo();
        _providerMock.Setup(p => p.CommitAndPushAsync(
                It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("nothing to commit, working tree clean"));

        var result = await _handler.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        pipeline.Get<PersistFailureKind>(ContextKeys.PersistFailureKind)
            .Should().Be(PersistFailureKind.NoChanges);
    }

    [Fact]
    public async Task ExecuteAsync_AuthFailureMessage_FailsWithAuthDeniedKind()
    {
        var pipeline = NewPipelineWithRepo();
        _providerMock.Setup(p => p.CommitAndPushAsync(
                It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("HTTP 401 Unauthorized"));

        var result = await _handler.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        pipeline.Get<PersistFailureKind>(ContextKeys.PersistFailureKind)
            .Should().Be(PersistFailureKind.AuthDenied);
    }

    [Fact]
    public async Task ExecuteAsync_DivergentRemoteMessage_FailsWithRemoteDivergentKind()
    {
        var pipeline = NewPipelineWithRepo();
        _providerMock.Setup(p => p.CommitAndPushAsync(
                It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("push rejected: non-fast-forward update"));

        var result = await _handler.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        pipeline.Get<PersistFailureKind>(ContextKeys.PersistFailureKind)
            .Should().Be(PersistFailureKind.RemoteDivergent);
    }

    [Fact]
    public async Task ExecuteAsync_HttpRequestException_FailsWithNetworkBlipKind()
    {
        var pipeline = NewPipelineWithRepo();
        _providerMock.Setup(p => p.CommitAndPushAsync(
                It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection reset by peer"));

        var result = await _handler.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        pipeline.Get<PersistFailureKind>(ContextKeys.PersistFailureKind)
            .Should().Be(PersistFailureKind.NetworkBlip);
    }

    [Fact]
    public async Task ExecuteAsync_UnclassifiedException_FailsWithUnknownKind()
    {
        var pipeline = NewPipelineWithRepo();
        _providerMock.Setup(p => p.CommitAndPushAsync(
                It.IsAny<Repository>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("disk full"));

        var result = await _handler.ExecuteAsync(NewContext(pipeline), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        pipeline.Get<PersistFailureKind>(ContextKeys.PersistFailureKind)
            .Should().Be(PersistFailureKind.Unknown);
    }

    private static PipelineContext NewPipelineWithRepo()
    {
        var pipeline = new PipelineContext();
        var repo = new Repository(
            "/tmp/work",
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
