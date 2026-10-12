using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using AgentSmith.Tests.TestHelpers;
using Moq;

namespace AgentSmith.Tests.Commands;

/// <summary>2026-10-12-3a36c: the spec PR opened at derivation is a failed run's persisted work.</summary>
public sealed class FailedRunSpecPullRequestTests
{
    private const string Url = "https://stub.test/sample-worker/pull/7";
    private static readonly RepoConnection[] Repos = [new() { Name = "sample-worker" }];

    private readonly Mock<ISourceProvider> _provider = new();
    private readonly FailedRunSpecPullRequest _sut;

    public FailedRunSpecPullRequestTests()
    {
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(_provider.Object);
        _sut = new FailedRunSpecPullRequest(factory.Object, new PullRequestOutcomePublisher(EventTestStubs.NoOp), NullLogger<FailedRunSpecPullRequest>.Instance);
    }

    [Fact]
    public async Task KeepAsync_SpecRepoSkipped_IsOpenedAndTheBodyRefreshedOnce()
    {
        var kept = await _sut.KeepAsync(SpecPipeline(), Repos,
            [new OpenedPullRequest("sample-worker", null, OpenStatus.SkippedNoChanges)], "banner body", CancellationToken.None);

        kept.Should().Be(new OpenedPullRequest("sample-worker", Url, OpenStatus.Opened));
        _provider.Verify(p => p.UpdatePullRequestBodyAsync(Url, "banner body", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task KeepAsync_SpecRepoAlreadyOpened_LeavesItAlone()
    {
        var kept = await _sut.KeepAsync(SpecPipeline(), Repos,
            [new OpenedPullRequest("sample-worker", Url, OpenStatus.Opened)], "banner body", CancellationToken.None);

        kept.Should().BeNull();
        _provider.Verify(p => p.UpdatePullRequestBodyAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task KeepAsync_NoSpecPullRequest_IsNull()
    {
        var kept = await _sut.KeepAsync(new PipelineContext(), Repos,
            [new OpenedPullRequest("sample-worker", null, OpenStatus.SkippedNoChanges)], "banner body", CancellationToken.None);

        kept.Should().BeNull();
    }

    private static PipelineContext SpecPipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecRepo, "sample-worker");
        pipeline.Set(ContextKeys.SpecPullRequestUrl, Url);
        return pipeline;
    }
}
