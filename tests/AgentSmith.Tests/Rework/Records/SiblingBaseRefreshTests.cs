using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Services.Branches;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static AgentSmith.Tests.Rework.Records.PrReviewRefreshHarness;

namespace AgentSmith.Tests.Rework.Records;

/// <summary>
/// 2026-11-17-023fb: who a landing refreshes — the project's other tickets with an open pull request on the repository
/// the landed ticket merged into, unless closed, running, queued or waiting; and nobody for a by-hand landing or a
/// project that did not turn refresh on.
/// </summary>
public sealed class SiblingBaseRefreshTests : IDisposable
{
    private const string OtherRepoPr = "https://github.com/o/web/pull/9";
    private readonly PrReviewRefreshHarness _h = new();
    private readonly Mock<IActiveRunLease> _leases = new();
    private readonly Mock<IPreviousAttemptReader> _attempts = new();
    private readonly Mock<ITicketClosedMarkStore> _closed = new();

    public SiblingBaseRefreshTests() =>
        _closed.Setup(c => c.OfAsync(It.IsAny<string>(), It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task OpenSiblingOnTheSameRepository_IsRefreshed()
    {
        await SeedAsync(Pr1, "7", PrReviewStates.Merged);
        await SeedAsync(Pr2, "8", PrReviewStates.Open);
        await SeedAsync(OtherRepoPr, "9", PrReviewStates.Open, repo: "web", repoUrl: "https://github.com/o/web");

        var work = await Sut().SiblingsAsync(Project, "7", CancellationToken.None);

        work.Should().ContainSingle().Which.LandedUrl.Should().Be(Pr1);
        work[0].Siblings.Select(s => s.TicketId).Should().Equal("8");
    }

    [Fact]
    public async Task ASiblingWithARunningRun_IsLeftToIt()
    {
        await SeedAsync(Pr1, "7", PrReviewStates.Merged);
        await SeedAsync(Pr2, "8", PrReviewStates.Open);
        _leases.Setup(l => l.GetByTicketAsync("p", new TicketId("8"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StaleLease("p", new TicketId("8"), "run-8", null, DateTimeOffset.UtcNow));

        (await Sut().SiblingsAsync(Project, "7", CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task AQueuedOrWaitingSibling_IsSkipped()
    {
        await SeedAsync(Pr1, "7", PrReviewStates.Merged);
        await SeedAsync(Pr2, "8", PrReviewStates.Open);
        _attempts.Setup(a => a.HasQueuedAsync("p", "8", It.IsAny<CancellationToken>())).ReturnsAsync(true);

        (await Sut().SiblingsAsync(Project, "7", CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task AClosedSibling_IsSkipped()
    {
        await SeedAsync(Pr1, "7", PrReviewStates.Merged);
        await SeedAsync(Pr2, "8", PrReviewStates.Open);
        _closed.Setup(c => c.OfAsync("gh-tracker", It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new TicketClosedMark("gh-tracker", "github-8", "8", "p", DateTimeOffset.UtcNow, "someone", null, [], false)]);

        (await Sut().SiblingsAsync(Project, "7", CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task ByHandLanding_OrRefreshOff_LaunchesNothing()
    {
        var config = new Mock<IConfigurationLoader>();
        config.Setup(c => c.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject> { ["p"] = Project },
        });

        await Sut(config.Object).LandedAsync(Landing(TicketLandingKinds.ByHand), CancellationToken.None);
        await Sut(config.Object).LandedAsync(Landing(TicketLandingKinds.Merged), CancellationToken.None);

        _h.Sources.Verify(s => s.Create(It.IsAny<RepoConnection>()), Times.Never, "no host is asked when nothing is refreshed");
    }

    private static TicketLandingFact Landing(string kind) =>
        new("gh-tracker", "github-7", "7", "p", kind, T, null, null, kind == TicketLandingKinds.Merged ? Pr1 : null);

    private SiblingBaseRefresh Sut(IConfigurationLoader? config = null) => new(
        _h.Store.ScopeFactory, config ?? Mock.Of<IConfigurationLoader>(), new ServerContext("unused.yml"),
        _leases.Object, _attempts.Object, _closed.Object, _h.Sources.Object, NullLogger<SiblingBaseRefresh>.Instance);

    private async Task SeedAsync(string url, string ticket, string state, string repo = "api", string repoUrl = "https://github.com/o/api")
    {
        await using var db = _h.Store.Context();
        db.Add(new PrReviewRecord
        {
            PrKey = PrKey.Of(url), Url = url, Repo = repo, RepoKey = RepoKey.Of(repoUrl), Number = url.Split('/')[^1],
            Project = "p", TicketId = ticket, RunId = $"run-{ticket}", State = state,
            StateAtTicks = T.AddHours(1).UtcTicks, CheckedAtTicks = T.AddHours(3).UtcTicks,
        });
        await db.SaveChangesAsync();
    }
}
