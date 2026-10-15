using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using static AgentSmith.Tests.Tickets.TicketStoreHarness;

namespace AgentSmith.Tests.Tickets;

/// <summary>
/// 2026-10-14-dbcei: a landing of either kind lists its ticket as done under the merge's time rule, after the
/// rule's earlier arms; the standings read landings through the configuration loaded once per read.
/// </summary>
public sealed class TicketLandedByHandListTests : IDisposable
{
    private readonly TicketStoreHarness _h = new();

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task TicketBucketRule_LandedByHandAfterTheNewestAttemptStarted_IsDone()
    {
        await _h.AddAsync(Run("r-1"), Step("r-1"));
        await ByHandAsync(T.AddHours(2));

        (await RowAsync()).Should().Match<TicketSummaryView>(r => r.Bucket == TicketBucket.Done && r.Since == T.AddHours(2));
    }

    [Fact]
    public async Task TicketBucketRule_AttemptStartedAfterTheByHandLanding_IsNotDone()
    {
        await _h.AddAsync(Run("r-1"), Step("r-1"));
        await ByHandAsync(T.AddHours(-1));

        (await RowAsync()).Bucket.Should().Be(TicketBucket.Idle, "an attempt started after the landing is that attempt's ticket");
    }

    [Fact]
    public async Task TicketList_TicketMarkedDoneByHand_IsInTheDoneBucket()
    {
        await _h.AddAsync(Run("r-1", status: "failed"), Step("r-1"), Record("1", PrReviewStates.Closed));
        await ByHandAsync(T.AddHours(2));

        var row = await RowAsync();
        var detail = await _h.Detail.ReadAsync("alpha", "42", default);

        (row.Bucket, row.Landing).Should().Be((TicketBucket.Done, TicketLanding.Unmerged), "the summary's landing still reads the pull requests");
        detail!.Summary.Bucket.Should().Be(TicketBucket.Done);
    }

    [Fact]
    public async Task TicketBucketRule_LandedByHandWithAnOpenPullRequest_StaysWaitingOnReview()
    {
        await _h.AddAsync(Run("r-1"), Step("r-1"), Record("1", PrReviewStates.Open));
        await ByHandAsync(T.AddHours(2));

        (await RowAsync()).Bucket.Should().Be(TicketBucket.WaitingOnReview);
    }

    [Fact]
    public async Task TicketStandings_ProjectNoLongerConfigured_ReadsNoLanding()
    {
        await ByHandAsync(T.AddHours(2));
        _h.Loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig());

        var run = Run("r-1");
        (await Standings().ReadAsync([run], default)).LandedAt(run).Should().BeNull();
    }

    [Fact]
    public async Task TicketStandings_ProjectAddedAfterStartup_ReadsItsLanding()
    {
        var live = Configured("alpha", "beta");
        _h.Loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(live);
        await ByHandAsync(T.AddHours(2), project: "beta", tracker: "beta-tracker");

        var run = Run("r-1", project: "beta");
        (await Standings().ReadAsync([run], default)).LandedAt(run).Should().Be(T.AddHours(2));
        _h.Config.Projects.Should().NotContainKey("beta", "the boot configuration never knew it");
    }

    [Fact]
    public async Task TicketStandings_HeadsOfThreeProjects_LoadTheConfigurationOnce()
    {
        _h.Loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(Configured("alpha", "beta", "gamma"));

        await Standings().ReadAsync([Run("r-1"), Run("r-2", project: "beta"), Run("r-3", project: "gamma")], default);

        _h.Loader.Verify(l => l.LoadConfig(It.IsAny<string>()), Times.Once());
    }

    private TicketStandings Standings() => _h.Services.GetRequiredService<TicketStandings>();

    private static AgentSmithConfig Configured(params string[] projects) => new()
    {
        Projects = projects.ToDictionary(p => p, p => new ResolvedProject
        {
            Name = p, Tracker = new TrackerConnection { Name = p == "alpha" ? "gh" : $"{p}-tracker", Type = TrackerType.GitHub },
        }),
    };

    private Task<bool> ByHandAsync(DateTimeOffset at, string project = "alpha", string tracker = "gh") =>
        _h.Services.GetRequiredService<ITicketLandingStore>().TryRecordAsync(new TicketLandingFact(
            tracker, "github-42", "42", project, TicketLandingKinds.ByHand, at, "Ada", "shipped by hand", null), default);

    private static PrReviewRecord Record(string number, string state) => new()
    {
        PrKey = PrKey.Of($"https://github.com/o/api/pull/{number}"), Url = $"https://github.com/o/api/pull/{number}", Repo = "api",
        Number = number, Project = "alpha", TicketId = "42", RunId = "r-1", State = state,
        StateAtTicks = T.AddHours(1).UtcTicks, CheckedAtTicks = T.AddHours(3).UtcTicks,
    };

    private async Task<TicketSummaryView> RowAsync() =>
        (await _h.List.ReadAsync(new TicketListRequest("alpha", null, null, null), default)).Tickets.Single();
}
