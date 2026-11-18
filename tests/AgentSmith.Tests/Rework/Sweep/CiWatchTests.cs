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
/// 2026-11-17-3435b: the CI loop's tick — open records of merge: on_ci_green projects whose CI is unread, on an older
/// head, pending for less than ci_pending_seconds or a verdict not acted on are nudged every ci_poll_seconds, one nudge
/// per ticket; acted verdicts, long-pending heads and other projects are not.
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
                ["ci"] = new() { Name = "ci", Merge = new MergePolicyConfig { When = MergeWhen.OnCiGreen, CiPollSeconds = 30 } },
                ["gate"] = new() { Name = "gate", Merge = new MergePolicyConfig { When = MergeWhen.OnGateGreen } },
            },
        });
    }

    [Fact]
    public async Task DueRecords_OfOnCiGreenProjects_AreNudged_OncePerTicket()
    {
        await SeedAsync("ci", "1", "a", ci: null);
        await SeedAsync("ci", "1", "b", ci: CiStates.Pending);
        await SeedAsync("ci", "2", "c", ci: CiStates.Green);
        await SeedAsync("gate", "3", "d", ci: null);

        (await Sut().CiOnceAsync(CancellationToken.None)).Should().Be(2);

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

    [Fact]
    public void Cadence_IsTheSmallestPoll_NullWithoutOnCiGreen()
    {
        CiWatchSelection.Cadence(CiWatchSelection.Watched(_loader.Object.LoadConfig("x").Projects.Values))
            .Should().Be(TimeSpan.FromSeconds(30));
        CiWatchSelection.Cadence([]).Should().BeNull();
    }

    private PrRecordReReadHostedService Sut() => new(_h.Store.Services, new ServerContext("agentsmith.yml"), _loader.Object, _h.Clock,
        NullLogger<PrRecordReReadHostedService>.Instance);

    private async Task SeedAsync(string project, string ticket, string number, string? ci, string? acted = null,
        TimeSpan? checkedAgo = null, TimeSpan? ciAgo = null)
    {
        var now = _h.Clock.GetUtcNow();
        await using var db = _h.Store.Context();
        var url = $"https://github.com/o/{project}/pull/{number}";
        db.Add(new PrReviewRecord
        {
            PrKey = PrKey.Of(url), Url = url, Repo = "api", Number = number, Project = project, TicketId = ticket, RunId = "run-1",
            State = PrReviewStates.Open, CheckedAtTicks = (now - (checkedAgo ?? TimeSpan.FromMinutes(1))).UtcTicks, HeadSha = "abc",
            CiState = ci, CiHeadSha = ci is null ? null : "abc", CiAtTicks = ci is null ? null : (now - (ciAgo ?? TimeSpan.FromMinutes(1))).UtcTicks,
            CiActed = acted,
        });
        await db.SaveChangesAsync();
    }

    public void Dispose() => _h.Dispose();
}
