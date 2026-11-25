using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Services.Sweep;
using AgentSmith.Tests.Rework.Records;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Rework.Sweep;

/// <summary>
/// 2026-11-17-3435b: the CI loop's tick — open records of review: none projects whose CI is unread, on an older
/// head, pending for less than ci_pending_seconds or a verdict not acted on are nudged every ci_poll_seconds, one nudge
/// per ticket; acted verdicts, long-pending heads and other projects are not.
/// 2026-11-23-849e: every project is watched while its CI is pending; only review: none keeps unread and unacted ones due.
/// </summary>
public sealed class CiWatchTests : IDisposable
{
    private readonly List<ReworkNudgeRequest> _nudged = [];
    private readonly Mock<IConfigurationLoader> _loader = new();
    private readonly PrReviewRefreshHarness _h;

    public CiWatchTests()
    {
        var nudges = new Mock<IReworkNudges>();
        nudges.Setup(n => n.EnqueueAsync(It.IsAny<ReworkNudgeRequest>(), It.IsAny<CancellationToken>()))
            .Callback((ReworkNudgeRequest r, CancellationToken _) => _nudged.Add(r)).Returns(Task.CompletedTask);
        _h = new PrReviewRefreshHarness(services => services.AddSingleton(nudges.Object));
        _loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["ci"] = new() { Name = "ci", Review = ReviewMode.None, Merge = new MergePolicyConfig { CiPollSeconds = 30 } },
                ["gate"] = new() { Name = "gate", Review = ReviewMode.Each },
            },
        });
    }

    [Fact]
    public async Task DueRecords_OfOnCiGreenProjects_AreNudged_OncePerTicket()
    {
        await SeedAsync("ci", "1", "a", ci: null);
        await SeedAsync("ci", "1", "b", ci: CiStates.Pending);
        await SeedAsync("ci", "2", "c", ci: CiStates.Green);
        await SeedAsync("gate", "3", "d", ci: null, checkedAgo: TimeSpan.FromMinutes(2));

        (await Sut().CiOnceAsync(CancellationToken.None)).Should().Be(2, "an unread record of another merge mode is the periodic re-read's");

        _nudged.Select(n => (n.Project, n.TicketId)).Should().BeEquivalentTo([("ci", "1"), ("ci", "2")]);
        _nudged.Should().AllSatisfy(n => n.Origin.Should().Be(ReworkNudgeOrigin.Refresh));
    }

    [Fact]
    public async Task ActedVerdicts_LongPendingHeads_AndFreshChecks_AreNotDue()
    {
        await SeedAsync("ci", "1", "a", ci: CiStates.Green, acted: "abc:green");
        await SeedAsync("ci", "2", "b", ci: CiStates.Pending, ciAgo: TimeSpan.FromHours(2));
        await SeedAsync("ci", "3", "c", ci: null, checkedAgo: TimeSpan.FromSeconds(5));

        (await Sut().CiOnceAsync(CancellationToken.None)).Should().Be(0);
    }

    // 2026-11-23-849e: a project that does not merge on green still has its pending CI read again; its verdicts stay read.
    [Fact]
    public async Task AnotherMergeMode_PendingIsDue_AVerdictIsNot()
    {
        await SeedAsync("gate", "4", "e", checkedAgo: TimeSpan.FromMinutes(2), ci: CiStates.Pending);
        await SeedAsync("gate", "5", "f", checkedAgo: TimeSpan.FromMinutes(2), ci: CiStates.Green);
        await SeedAsync("gate", "6", "g", checkedAgo: TimeSpan.FromMinutes(2), ci: CiStates.Red);

        (await Sut().CiOnceAsync(CancellationToken.None)).Should().Be(1);

        _nudged.Select(n => (n.Project, n.TicketId)).Should().BeEquivalentTo([("gate", "4")]);
    }

    [Fact]
    public void Cadence_IsTheSmallestPoll_NullWithoutProjects()
    {
        CiWatchSelection.Cadence(CiWatchSelection.Watched(_loader.Object.LoadConfig("x").Projects.Values))
            .Should().Be(TimeSpan.FromSeconds(30));
        CiWatchSelection.Cadence([]).Should().BeNull();
    }

    private PrRecordReReadHostedService Sut() => new(_h.Store.Services, new ServerContext("agentsmith.yml"), _loader.Object, _h.Clock,
        NullLogger<PrRecordReReadHostedService>.Instance);

    // 2026-11-25-9992i: acting is the project's review — a review: none project's verdicts are acted on into any branch, a
    // series branch included; another review's are not.
    [Fact]
    public async Task ASeriesBranchRecord_IsActedOnByTheProjectsReview()
    {
        await SeedAsync("ci", "7", "h", checkedAgo: TimeSpan.FromMinutes(2), ci: CiStates.Green, target: "agent-smith/series/sr-1");
        await SeedAsync("gate", "8", "i", checkedAgo: TimeSpan.FromMinutes(2), ci: CiStates.Green, target: "agent-smith/series/sr-1");

        (await Sut().CiOnceAsync(CancellationToken.None)).Should().Be(1);

        _nudged.Select(n => (n.Project, n.TicketId)).Should().BeEquivalentTo([("ci", "7")]);
    }

    // 2026-11-25-9992e: a ticket an active series holds is read at every poll while its CI waits — unread, pending past
    // ci_pending_seconds, in any mode — and its verdicts are its master's, so an unacted one is not due.
    [Fact]
    public async Task ASeriesTicket_IsDueWhileItsCiWaits_InEveryMode_ItsVerdictIsNot()
    {
        await _h.Series.OpenAsync(AgentSmith.Tests.Series.SeriesRecords.Running() with
        {
            Project = "gate", Tickets = [AgentSmith.Tests.Series.SeriesRecords.Ticket("10"), AgentSmith.Tests.Series.SeriesRecords.Ticket("11")],
        }, default);
        await _h.Series.OpenAsync(AgentSmith.Tests.Series.SeriesRecords.Running("sr-2", "conv-2") with
        {
            Project = "ci", Tickets = [AgentSmith.Tests.Series.SeriesRecords.Ticket("12")],
        }, default);
        await SeedAsync("gate", "10", "j", checkedAgo: TimeSpan.FromMinutes(2), ci: CiStates.Pending, ciAgo: TimeSpan.FromHours(2));
        await SeedAsync("gate", "11", "k", checkedAgo: TimeSpan.FromMinutes(2), ci: null);
        await SeedAsync("ci", "12", "l", ci: CiStates.Green);

        (await Sut().CiOnceAsync(CancellationToken.None)).Should().Be(2);

        _nudged.Select(n => (n.Project, n.TicketId)).Should().BeEquivalentTo([("gate", "10"), ("gate", "11")]);
    }

    [Fact]
    public void SeriesTickets_AreTheProjectsActiveSeriesMembers()
    {
        var series = AgentSmith.Tests.Series.SeriesRecords.Running();

        CiWatchSelection.SeriesTickets([series, series with { Id = "sr-2" }], "sample").Should().BeEquivalentTo(["43", "44", "45"]);
        CiWatchSelection.SeriesTickets([series], "other").Should().BeEmpty();
    }

    private async Task SeedAsync(string project, string ticket, string number, string? ci, string? acted = null,
        TimeSpan? checkedAgo = null, TimeSpan? ciAgo = null, string? target = null)
    {
        var now = _h.Clock.GetUtcNow();
        await using var db = _h.Store.Context();
        var url = $"https://github.com/o/{project}/pull/{number}";
        db.Add(new PrReviewRecord
        {
            PrKey = PrKey.Of(url), Url = url, Repo = "api", Number = number, Project = project, TicketId = ticket, RunId = "run-1",
            State = PrReviewStates.Open, CheckedAtTicks = (now - (checkedAgo ?? TimeSpan.FromMinutes(1))).UtcTicks, HeadSha = "abc",
            CiState = ci, CiHeadSha = ci is null ? null : "abc", CiAtTicks = ci is null ? null : (now - (ciAgo ?? TimeSpan.FromMinutes(1))).UtcTicks,
            CiActed = acted, TargetBranch = target,
        });
        await db.SaveChangesAsync();
    }

    public void Dispose() => _h.Dispose();
}
