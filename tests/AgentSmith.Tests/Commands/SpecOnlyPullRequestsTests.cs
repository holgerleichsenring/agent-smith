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

/// <summary>2026-10-12-3a36c/f5f7e: a series repository's spec PR is the run's work whether or not it gained code.</summary>
public sealed class SpecOnlyPullRequestsTests
{
    private const string Url = "https://stub.test/sample-worker/pull/7";
    private const string ClientUrl = "https://stub.test/sample-client/pull/3";
    private static readonly RepoConnection[] Repos = [new() { Name = "sample-worker" }, new() { Name = "sample-client" }];

    private readonly Mock<ISourceProvider> _provider = new();
    private readonly SpecOnlyPullRequests _sut;

    public SpecOnlyPullRequestsTests()
    {
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(_provider.Object);
        _sut = new SpecOnlyPullRequests(factory.Object, new PullRequestOutcomePublisher(EventTestStubs.NoOp), NullLogger<SpecOnlyPullRequests>.Instance);
    }

    [Fact]
    public async Task KeepAsync_SpecRepoSkipped_IsOpenedAndTheBodyRefreshedOnce()
    {
        var kept = await _sut.KeepAsync(LegacyPipeline(), Repos,
            [new OpenedPullRequest("sample-worker", null, OpenStatus.SkippedNoChanges)], "banner body", false, CancellationToken.None);

        kept.Should().Equal(new OpenedPullRequest("sample-worker", Url, OpenStatus.Opened));
        _provider.Verify(p => p.UpdatePullRequestBodyAsync(Url, "banner body", It.IsAny<CancellationToken>()), Times.Once);
        _provider.Verify(p => p.MarkPullRequestReadyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task KeepAsync_SpecRepoAlreadyOpened_LeavesItAlone()
    {
        var kept = await _sut.KeepAsync(LegacyPipeline(), Repos,
            [new OpenedPullRequest("sample-worker", Url, OpenStatus.Opened)], "banner body", true, CancellationToken.None);

        kept.Should().BeEmpty();
        _provider.Verify(p => p.UpdatePullRequestBodyAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task KeepAsync_NoSpecPullRequest_KeepsNothing()
    {
        var kept = await _sut.KeepAsync(new PipelineContext(), Repos,
            [new OpenedPullRequest("sample-worker", null, OpenStatus.SkippedNoChanges)], "banner body", false, CancellationToken.None);

        kept.Should().BeEmpty();
    }

    [Fact]
    public async Task KeepAsync_TwoSpecOnlyReposAndReady_KeepsBothAndPromotesThem()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecPullRequestUrls,
            new Dictionary<string, string> { ["sample-worker"] = Url, ["sample-client"] = ClientUrl });

        var kept = await _sut.KeepAsync(pipeline, Repos,
            [new OpenedPullRequest("sample-worker", null, OpenStatus.SkippedNoChanges),
             new OpenedPullRequest("sample-client", null, OpenStatus.SkippedNoChanges)], "body", true, CancellationToken.None);

        kept.Select(k => k.Url).Should().BeEquivalentTo([Url, ClientUrl]);
        _provider.Verify(p => p.MarkPullRequestReadyAsync(Url, It.IsAny<CancellationToken>()), Times.Once);
        _provider.Verify(p => p.MarkPullRequestReadyAsync(ClientUrl, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static PipelineContext LegacyPipeline()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecRepo, "sample-worker");
        pipeline.Set(ContextKeys.SpecPullRequestUrl, Url);
        return pipeline;
    }
}
