using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Deployment;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Deployment;

/// <summary>2026-11-18-3863f: one followed deploy read from its host — a pull request's state and its CI on the head
/// that read saw, or a pipeline run — and settled when the host says so.</summary>
public sealed class DeployFollowTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-11-18T10:05:00Z");
    private readonly Mock<ISourceProvider> _host = new();
    private readonly Mock<IPipelineRunner> _runner = new();

    public DeployFollowTests()
    {
        _host.As<IPrReviewStateReader>().Setup(s => s.ReadAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PrReviewState("2", PrReviewStates.Merged, Now, [], "head1"));
        _host.As<IPullRequestCiReader>().Setup(c => c.ReadCiAsync(It.IsAny<string>(), It.IsAny<CiReadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullRequestCi(CiStates.Red, "head1", Now, [new CiFailure("kubeconform", "https://ci.example/checks/9", "invalid replicas")]));
    }

    [Fact]
    public async Task Follow_PullRequestWithAFailedCheck_RecordsTheCheckItsExcerptAndLinkOnTheHeadItRead()
    {
        var record = Record(pullRequest: true);

        await Follow().FollowAsync(record, DeployFixture.Project(), Now, CancellationToken.None);

        record.PullRequestState.Should().Be(PrReviewStates.Merged);
        record.CiState.Should().Be(CiStates.Red);
        DeployRecordRepository.View(record).Failures.Single().Should().Be(new CiFailure("kubeconform", "https://ci.example/checks/9", "invalid replicas"));
        record.SettledAtTicks.Should().Be(Now.UtcTicks, "merged with a CI that is no longer pending");
        _host.As<IPullRequestCiReader>().Verify(c => c.ReadCiAsync(It.IsAny<string>(), It.Is<CiReadRequest>(r => r.HeadSha == "head1"),
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public async Task Follow_PipelineRun_RecordsItsStateAndStaysFollowedWhilePending()
    {
        _runner.Setup(r => r.ReadAsync("42", It.IsAny<CiReadRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PipelineRunState(CiStates.Pending, "https://ci.example/runs/42", []));
        var record = Record(pullRequest: false);

        await Follow().FollowAsync(record, DeployFixture.Project(), Now, CancellationToken.None);

        record.CiState.Should().Be(CiStates.Pending);
        record.CheckedAtTicks.Should().Be(Now.UtcTicks);
        record.SettledAtTicks.Should().BeNull();
    }

    [Fact]
    public async Task Follow_BindingNoLongerResolvesOrProjectGone_SettlesWithTheReason()
    {
        var unbound = Record(pullRequest: true);
        unbound.Environment = "staging";
        var orphan = Record(pullRequest: true);

        await Follow().FollowAsync(unbound, DeployFixture.Project(), Now, CancellationToken.None);
        await Follow().FollowAsync(orphan, null, Now, CancellationToken.None);

        unbound.Note.Should().Contain("binding no longer resolves to `gitops`");
        orphan.Note.Should().Contain("no longer configured");
        orphan.SettledAtTicks.Should().NotBeNull();
    }

    private DeployFollow Follow()
    {
        var sources = new Mock<ISourceProviderFactory>();
        sources.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(_host.Object);
        var runners = new Mock<IPipelineRunnerFactory>();
        runners.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(_runner.Object);
        return new DeployFollow(sources.Object, runners.Object, NullLogger<DeployFollow>.Instance);
    }

    private static DeployRecord Record(bool pullRequest) => new()
    {
        RunId = "r-1", Project = "shop", Context = "api", Environment = "prod", Version = "v1", Repo = "gitops",
        Url = pullRequest ? "https://stub.test/gitops/pull/2" : "https://ci.example/runs/42",
        PullRequestUrl = pullRequest ? "https://stub.test/gitops/pull/2" : null, PipelineRunId = pullRequest ? null : "42",
        CiState = CiStates.Pending, DeliveredAtTicks = Now.AddMinutes(-5).UtcTicks,
    };
}
