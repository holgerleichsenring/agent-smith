using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Services.Sweep;
using AgentSmith.Tests.Rework.Records;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Rework.Sweep;

/// <summary>
/// 2026-10-14-dbceh: the five-minute re-read recovers a wake lost after a landing's insert — a dependent freed
/// by a landing WRITTEN in the last day, filed completely, StartsWhenMet, never attempted, not queued, not
/// leased and not landed itself gets one RequirementsMet nudge; newest landing first, at most fifty.
/// </summary>
public sealed class DependentsRecoveryTests : IDisposable
{
    private readonly List<ReworkNudgeRequest> _nudged = [];
    private readonly Mock<IConfigurationLoader> _loader = new();
    private readonly PrReviewRefreshHarness _h;

    public DependentsRecoveryTests()
    {
        var nudges = new Mock<IReworkNudges>();
        nudges.Setup(n => n.EnqueueAsync(It.IsAny<ReworkNudgeRequest>(), It.IsAny<CancellationToken>()))
            .Callback((ReworkNudgeRequest r, CancellationToken _) => _nudged.Add(r)).Returns(Task.CompletedTask);
        _h = new PrReviewRefreshHarness(services => services.AddSingleton(nudges.Object).AddSingleton(_ => _h!.Wake()));
        _loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject> { ["p"] = PrReviewRefreshHarness.Project },
        });
    }

    public void Dispose() => _h.Dispose();

    private DateTimeOffset Now => _h.Clock.GetUtcNow();

    private IEnumerable<string> Woken => _nudged.Where(n => n.Origin == ReworkNudgeOrigin.RequirementsMet).Select(n => n.TicketId);

    [Fact]
    public async Task ReRead_HeldDependentAllLandedNoRun_EnqueuesOneRequirementsMetNudge()
    {
        await DependentAsync("8", "7", "6");
        await LandAsync("7", TimeSpan.FromHours(2));
        await LandAsync("6", TimeSpan.FromHours(1));

        await CycleAsync();

        Woken.Should().Equal("8");
    }

    [Fact]
    public async Task ReRead_DependentAlreadyAttemptedQueuedOrLeased_IsNotNudged()
    {
        foreach (var dependent in new[] { "8", "9", "10", "11" }) await DependentAsync(dependent, "7");
        await LandAsync("7", TimeSpan.FromHours(1));
        _h.Attempts.Setup(a => a.LatestAsync("p", "8", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreviousAttempt("run-8", RunStatuses.Success, Now, true));
        _h.Attempts.Setup(a => a.HasQueuedAsync("p", "9", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        _h.Leases.Setup(l => l.GetByTicketAsync("p", new TicketId("10"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StaleLease("p", new TicketId("10"), "run-10", null, Now));

        await CycleAsync();

        Woken.Should().Equal("11");
    }

    [Fact]
    public async Task ReRead_DependentWithoutStartsWhenMetOrOfAnUnconfiguredProject_IsNotNudged()
    {
        await DependentAsync("8", "7", startsWhenMet: false);
        await DependentAsync("9", "7", project: "elsewhere");
        await DependentAsync("10", "7");
        await LandAsync("7", TimeSpan.FromHours(1));

        await CycleAsync();

        Woken.Should().Equal("10");
    }

    [Fact]
    public async Task ReRead_LandingRecordedMoreThanADayAgo_IsNotRecovered()
    {
        await DependentAsync("8", "7");
        await LandAsync("7", TimeSpan.FromHours(25));

        await CycleAsync();

        Woken.Should().BeEmpty();
    }

    [Fact]
    public async Task ReRead_LandingRecordedTodayOfAnOldMerge_IsRecovered()
    {
        await DependentAsync("8", "7");
        await LandAsync("7", TimeSpan.FromHours(1), landedAt: Now.AddDays(-30));

        await CycleAsync();

        Woken.Should().Equal(["8"], "the window is the row's write time, not the host's merge time");
    }

    [Fact]
    public async Task ReRead_DependentLandedByHand_IsNotNudged()
    {
        await DependentAsync("8", "7");
        await LandAsync("7", TimeSpan.FromHours(2));
        await LandAsync("8", TimeSpan.FromHours(1), TicketLandingKinds.ByHand);

        await CycleAsync();

        Woken.Should().BeEmpty();
    }

    [Fact]
    public async Task ReRead_SixtyFreedDependents_NudgesTheFiftyOfTheNewestLandings()
    {
        for (var i = 1; i <= 60; i++)
        {
            await DependentAsync($"d{i}", $"r{i}");
            await LandAsync($"r{i}", TimeSpan.FromMinutes(120 - i));
        }

        await CycleAsync();

        Woken.Should().BeEquivalentTo(Enumerable.Range(11, 50).Select(i => $"d{i}"));
    }

    private Task<int> CycleAsync() =>
        new PrRecordReReadHostedService(_h.Store.Services, new ServerContext("agentsmith.yml"), _loader.Object, _h.Clock,
            NullLogger<PrRecordReReadHostedService>.Instance).ReReadOnceAsync(CancellationToken.None);

    private async Task DependentAsync(string dependent, string required, string? second = null, bool startsWhenMet = true,
        string project = "p")
    {
        await using var db = _h.Store.Context();
        foreach (var r in second is null ? [required] : new[] { required, second })
            db.Add(new TicketRequirement
            {
                Tracker = "gh-tracker", TicketKey = $"github-{dependent}", TicketId = dependent, Project = project,
                RequiredTicketKey = $"github-{r}", RequiredTicketId = r, StartsWhenMet = startsWhenMet, FiledInConversation = "s-1",
            });
        await db.SaveChangesAsync();
        await _h.Approvals.SaveAsync(ApprovedSets.Record($"github-{dependent}", Now, tracker: "gh-tracker"), default);
    }

    private async Task LandAsync(string ticket, TimeSpan writtenAgo, string kind = TicketLandingKinds.Merged, DateTimeOffset? landedAt = null)
    {
        await using var db = _h.Store.Context();
        using (db.SuspendAuditStamping())
        {
            db.Add(new LandedTicket
            {
                Tracker = "gh-tracker", TicketKey = $"github-{ticket}", TicketId = ticket, Project = "p", Kind = kind,
                LandedAt = landedAt ?? Now - writtenAgo, CreatedAt = Now - writtenAgo, UpdatedAt = Now - writtenAgo,
            });
            await db.SaveChangesAsync();
        }
    }
}
