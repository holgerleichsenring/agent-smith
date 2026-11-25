using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Commands;

/// <summary>2026-11-23-0219: the re-queue step asks a provider that re-queues, answers 0 for one that cannot or a local
/// repository, and a failed re-queue changes nothing.</summary>
public sealed class PullRequestBuildRequeueTests
{
    private const string Url = "https://dev.azure.com/o/p/_git/api/pullrequest/9";
    private readonly Mock<ISourceProvider> _provider = new();
    private readonly Mock<ISourceProviderFactory> _sources = new();

    public PullRequestBuildRequeueTests() => _sources.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(() => _provider.Object);

    private PullRequestBuildRequeue Sut => new(_sources.Object, NullLogger<PullRequestBuildRequeue>.Instance);

    [Fact]
    public async Task RequeueAsync_AProviderThatRequeues_AnswersItsCount()
    {
        _provider.As<IPullRequestBuildRequeuer>().Setup(r => r.RequeueNotStartedAsync(Url, It.IsAny<bool>(), default)).ReturnsAsync(2);

        (await Sut.RequeueAsync(new RepoConnection { Name = "api", Type = RepoType.AzureDevOps }, Url, default)).Should().Be(2);
    }

    // 2026-11-25-7a92b: unsettled unless the caller vouches for the head.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RequeueAsync_TheSettledFlag_ReachesTheProvider(bool settled)
    {
        _provider.As<IPullRequestBuildRequeuer>().Setup(r => r.RequeueNotStartedAsync(Url, settled, default)).ReturnsAsync(1);

        var queued = settled ? await Sut.RequeueAsync(_provider.Object, Url, default, headSettled: true) : await Sut.RequeueAsync(_provider.Object, Url, default);

        queued.Should().Be(1);
    }

    [Fact]
    public async Task RequeueAsync_AProviderWithoutRequeueOrALocalRepo_IsZero()
    {
        (await Sut.RequeueAsync(_provider.Object, Url, default)).Should().Be(0);
        (await Sut.RequeueAsync(new RepoConnection { Name = "api", Type = RepoType.Local }, Url, default)).Should().Be(0);
        _sources.Verify(f => f.Create(It.IsAny<RepoConnection>()), Times.Never);
    }

    [Fact]
    public async Task RequeueAsync_AHostThatThrows_IsZero()
    {
        _provider.As<IPullRequestBuildRequeuer>().Setup(r => r.RequeueNotStartedAsync(Url, It.IsAny<bool>(), default))
            .ThrowsAsync(new InvalidOperationException("TF400813"));

        (await Sut.RequeueAsync(_provider.Object, Url, default)).Should().Be(0);
    }
}
