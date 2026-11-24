using AgentSmith.Application.Services.BaseRefresh;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Handlers.BaseRefresh;

/// <summary>2026-11-24-5ee0: a pushed refresh resolves its earlier refusals, then queues the builds that did not start.</summary>
public sealed class RefreshedPullRequestFollowUpTests
{
    private const string Url = "https://dev.azure.com/o/p/_git/r/pullrequest/5";
    private readonly Mock<ISourceProvider> _provider = new();
    private readonly List<string> _calls = [];
    private readonly RepoConnection _repo = new() { Name = "server", Type = RepoType.AzureDevOps };

    public RefreshedPullRequestFollowUpTests()
    {
        _provider.As<IPrMarkedThreadResolver>().Setup(r => r.ResolveMarkedThreadsAsync("5", RefreshBranchBaseHandler.Marker, It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("resolve")).ReturnsAsync(1);
        _provider.As<IPullRequestBuildRequeuer>().Setup(q => q.RequeueNotStartedAsync(Url, It.IsAny<CancellationToken>()))
            .Callback(() => _calls.Add("requeue")).ReturnsAsync(1);
    }

    [Fact]
    public async Task RunAsync_Push_ResolvesThenRequeues()
    {
        var said = await Sut().RunAsync(_repo, Url, "5", CancellationToken.None);

        _calls.Should().Equal("resolve", "requeue");
        said.Should().Be(" Resolved 1 earlier refusal thread(s). Queued 1 required build(s) that had not started.");
    }

    [Fact]
    public async Task RunAsync_AFailingResolve_StillRequeues()
    {
        _provider.As<IPrMarkedThreadResolver>().Setup(r => r.ResolveMarkedThreadsAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("403"));

        var said = await Sut().RunAsync(_repo, Url, "5", CancellationToken.None);

        _calls.Should().Equal("requeue");
        said.Should().Contain("Queued 1").And.NotContain("Resolved");
    }

    [Fact]
    public async Task RunAsync_AHostTimeoutInTheRequeue_DoesNotFailThePush()
    {
        _provider.As<IPullRequestBuildRequeuer>().Setup(q => q.RequeueNotStartedAsync(Url, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("HttpClient.Timeout"));

        (await Sut().RunAsync(_repo, Url, "5", CancellationToken.None)).Should().Contain("Resolved 1").And.NotContain("Queued");
    }

    [Fact]
    public async Task RunAsync_HostWithNeither_SaysNothing() =>
        (await Sut(new Mock<ISourceProvider>().Object).RunAsync(_repo, Url, "5", CancellationToken.None)).Should().BeEmpty();

    [Fact]
    public async Task RunAsync_LocalRepoOrNoPullRequest_DoesNothing()
    {
        (await Sut().RunAsync(new RepoConnection { Name = "l", Type = RepoType.Local }, Url, "5", CancellationToken.None)).Should().BeEmpty();
        (await Sut().RunAsync(_repo, null, null, CancellationToken.None)).Should().BeEmpty();
        _calls.Should().BeEmpty();
    }

    private RefreshedPullRequestFollowUp Sut(ISourceProvider? provider = null)
    {
        var sources = new Mock<ISourceProviderFactory>();
        sources.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(provider ?? _provider.Object);
        return new RefreshedPullRequestFollowUp(sources.Object,
            new PullRequestBuildRequeue(sources.Object, NullLogger<PullRequestBuildRequeue>.Instance),
            NullLogger<RefreshedPullRequestFollowUp>.Instance);
    }
}
