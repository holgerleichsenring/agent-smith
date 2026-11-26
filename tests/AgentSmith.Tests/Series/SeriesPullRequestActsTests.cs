using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Series;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Tickets;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Acts;
using AgentSmith.Server.Services.Branches;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Server.Services.Series;
using AgentSmith.Server.Services.Tickets;
using AgentSmith.Tests.Tickets;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-25-9992e: the series master's acts on a ticket's pull requests, over a real migrated store: the merge is
/// the series page's, asked by the series master with its protection and judged; the refresh starts a refresh-base run per
/// open pull request unless one is at work; the retarget is a retarget run; the CI read answers the head's CI after a host
/// read, requeue and start are a merge-green run carrying NotStarted or NoCi.</summary>
public sealed class SeriesPullRequestActsTests : IDisposable
{
    private static readonly DateTimeOffset T = TicketStoreHarness.T;
    private const string Pr = "https://github.com/o/api/pull/1";
    private readonly Mock<IMergeGreenRun> _merges = new();
    private readonly Mock<IPrRecordRefreshNow> _refresh = new();
    private readonly Mock<IActRun> _acts = new();
    private readonly Mock<ISourceProvider> _host = new();
    private readonly List<CiGreenMergeRequest> _asked = [];
    private readonly TicketStoreHarness _h;

    public SeriesPullRequestActsTests()
    {
        _h = new(services =>
        {
            services.AddTransient<SeriesRunOrder>().AddTransient<SeriesRunPullRequests>().AddTransient<SeriesRunTickets>();
            services.AddScoped<SeriesRunProjectReads>().AddScoped<SeriesRunNames>().AddScoped<SeriesRunCosts>()
                .AddScoped<SeriesRunDetailReader>().AddScoped<SeriesMergeNextAct>().AddSingleton(_merges.Object)
                .AddSingleton(_refresh.Object).AddScoped<SeriesParkedQuestion>().AddScoped<SeriesStatusReader>().AddTransient<SeriesRunHolds>();
        });
        _h.Config.Projects["alpha"] = new()
        {
            Name = "alpha", Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
            Repos = [new RepoConnection { Name = "api", DefaultBranch = "main", Url = "https://github.com/o/api" }],
        };
        _h.Sources.Setup(s => s.Create(It.IsAny<RepoConnection>())).Returns(_host.Object);
        _merges.Setup(m => m.RunAsync(It.IsAny<ResolvedProject>(), It.IsAny<CiGreenMergeRequest>(), It.IsAny<CancellationToken>(),
                It.IsAny<ActSlot<IReadOnlyList<PullRequestMergeLine>>?>()))
            .Callback<ResolvedProject, CiGreenMergeRequest, CancellationToken, ActSlot<IReadOnlyList<PullRequestMergeLine>>?>((_, r, _, slot) =>
            {
                _asked.Add(r);
                slot?.Set([new("api", PullRequestCompletionOutcome.Armed, $"api: armed — auto-complete set ({Pr})")]);
            })
            .ReturnsAsync(true);
    }

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task Merge_IsTheSeriesPagesMerge_AskedByTheMaster_CheckedWithItsProtection()
    {
        var series = await SeedAsync();

        var said = await ActsAsync(async (acts, ct) => (await acts.MergeAsync(series, "1", BranchProtection.Respect, null, ct)).Answer);

        said.Should().Be($"api: armed — auto-complete set ({Pr})");
        _asked.Should().ContainSingle().Which.Should().Match<CiGreenMergeRequest>(r =>
            r.AskedBy == "agent-smith (series master)" && !r.Judged && r.ChecksTarget && r.Protection == BranchProtection.Respect);
    }

    // 2026-11-25-96aeb: an override judges the master's merge and carries its reason; the target is still checked.
    [Fact]
    public async Task Merge_WithAnOverride_IsJudged_AndCarriesItsReason()
    {
        var series = await SeedAsync();

        await ActsAsync(async (acts, ct) => (await acts.MergeAsync(series, "1", BranchProtection.Bypass, "41 only renames", ct)).Answer);

        _asked.Should().ContainSingle().Which.Should().Match<CiGreenMergeRequest>(r =>
            r.Judged && r.ChecksTarget && r.OverrideReason == "41 only renames");
    }

    [Fact]
    public async Task Refresh_StartsARefreshPerOpenPullRequest_UnlessTheTicketHasNone()
    {
        var series = await SeedAsync();
        _host.Setup(h => h.ReadPullRequestBaseAsync(Pr, It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        (await ActsAsync((acts, ct) => acts.RefreshAsync(series, "1", ct))).Should().StartWith($"{Pr}: not refreshed — The host named no base");
        (await ActsAsync((acts, ct) => acts.RefreshAsync(series, "2", ct))).Should().Be("Ticket 2 has no open pull request to refresh.");
    }

    [Fact]
    public async Task Retarget_MovesTheOpenPullRequestThroughARetargetRun()
    {
        var series = await SeedAsync();
        _host.Setup(h => h.ReadPullRequestBaseAsync(Pr, It.IsAny<CancellationToken>())).ReturnsAsync("agent-smith/0");
        _acts.Setup(a => a.RunAsync("alpha", PipelinePresets.RetargetName, It.IsAny<IReadOnlyDictionary<string, object>>(), It.IsAny<TicketId?>(), null))
            .Callback<string, string, IReadOnlyDictionary<string, object>, TicketId?, string?>((_, _, seeds, _, _) =>
                ((ActSlot<int>)seeds[ContextKeys.ActOutcome]).Set(1))
            .ReturnsAsync(CommandResult.Ok("moved"));

        (await ActsAsync((acts, ct) => acts.RetargetAsync(series, "1", "api", "main", ct))).Should().Be($"{Pr} now targets main (was agent-smith/0).");
        (await ActsAsync((acts, ct) => acts.RetargetAsync(series, "1", "api", "agent-smith/0", ct))).Should().Contain("already targets");
    }

    [Fact]
    public async Task Ci_ReadReadsTheHostFirst_RequeueAndStartRunAMergeGreenRun()
    {
        var series = await SeedAsync();

        (await ActsAsync((acts, ct) => acts.CiAsync(series, "1", SeriesCiActs.Read, ct))).Should().Be($"api {Pr} into main: CI green.");
        _refresh.Verify(r => r.RefreshAsync(It.Is<ResolvedProject>(p => p.Name == "alpha"), "1"), Times.Once);
        await ActsAsync((acts, ct) => acts.CiAsync(series, "1", SeriesCiActs.Requeue, ct));
        await ActsAsync((acts, ct) => acts.CiAsync(series, "1", SeriesCiActs.Start, ct));

        _asked.Select(r => (r.PullRequests[0].NotStarted, r.PullRequests[0].NoCi)).Should().Equal((true, false), (false, true));
    }

    // 2026-11-26-5201f: a pull request on a merged prerequisite's branch has no build policy there and its CI is not read — a
    // start or a requeue says so and the way on, instead of "succeeding" with nothing running.
    [Theory]
    [InlineData(SeriesCiActs.Start)]
    [InlineData(SeriesCiActs.Requeue)]
    public async Task Ci_StartOnABranchNothingValidates_SaysRetarget_AndRunsNothing(string act)
    {
        var series = await SeedAsync(target: "agent-smith/19654");

        var said = await ActsAsync((acts, ct) => acts.CiAsync(series, "1", act, ct));

        said.Should().Be($"api {Pr} targets agent-smith/19654, neither the series nor the default branch: no build policy validates it "
            + "there and its CI is not read, so nothing would run. Retarget it with retarget_pull_request first.");
        _asked.Should().BeEmpty();
    }

    // 2026-11-26-49d3a: a red head is read with every failed check's evidence as its record stored it.
    [Fact]
    public async Task Ci_ReadOfARedHead_AnswersEveryFailedChecksEvidence()
    {
        var series = await SeedAsync(failures:
        [
            new CiFailure("PR build", "https://ci.example/812", "test failed: OrderImportTests.Imports_the_batch\nExpected 12 orders, found 11."),
            new CiFailure("lint", null, "2 warnings treated as errors"),
        ]);

        var said = await ActsAsync((acts, ct) => acts.CiAsync(series, "1", SeriesCiActs.Read, ct));

        said.Should().StartWith($"api {Pr} into main: CI red — PR build: test failed: OrderImportTests.Imports_the_batch.")
            .And.Contain($"### PR build (api, {Pr})\nFull log: https://ci.example/812")
            .And.Contain("Expected 12 orders, found 11.").And.Contain($"### lint (api, {Pr})").And.Contain("2 warnings treated as errors");
    }

    private async Task<SeriesRunRecord> SeedAsync(string target = "main", IReadOnlyList<CiFailure>? failures = null)
    {
        var series = new SeriesRunRecord("sr-1", "conv-sr-1", "alpha", "operator", true, SeriesRunStates.Running,
            [.. new[] { "1", "2" }.Select(id => new SeriesTicket(id, $"SCRUM-{id}", $"Ticket {id}", false, T))], T);
        await _h.Services.GetRequiredService<ISeriesRunStore>().OpenAsync(series, default);
        await _h.AddAsync(new PrReviewRecord
        {
            PrKey = PrKey.Of(Pr), Url = Pr, Repo = "api", RepoKey = RepoKey.Of("https://github.com/o/api"), Number = "1",
            Project = "alpha", TicketId = "1", RunId = "run-1", State = PrReviewStates.Open, HeadSha = "abc", TargetBranch = target,
            CheckedAtTicks = T.UtcTicks, CiState = failures is null ? CiStates.Green : CiStates.Red, CiHeadSha = "abc", CiFailuresJson = PrRecordCiJson.Failures(failures ?? []),
        }, TicketStoreHarness.Run("run-1", ticket: "1"));
        return series;
    }

    private async Task<string> ActsAsync(Func<ISeriesPullRequestActs, CancellationToken, Task<string>> act)
    {
        await using var scope = _h.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var (projects, records, attempts) = (sp.GetRequiredService<TicketProjects>(), sp.GetRequiredService<PrReviewRecordRepository>(),
            sp.GetRequiredService<IPreviousAttemptReader>());
        var free = new RefreshableTicket(_h.Lease.Object, attempts, sp.GetRequiredService<ITicketClosedMarkStore>());
        var refreshes = new BaseRefreshAsked(sp.GetRequiredService<IServiceScopeFactory>(), new BaseRefreshStart(free, NullLogger<BaseRefreshStart>.Instance),
            _h.Sources.Object);
        var acts = new SeriesPullRequestActs(sp.GetRequiredService<SeriesMergeNextAct>(), projects, records, refreshes,
            new PullRequestRetargetRun(_acts.Object), _h.Sources.Object, new SeriesTicketCi(projects, records, _refresh.Object, attempts, _merges.Object, new AgentSmith.Server.Services.Series.SeriesRunPullRequests()));
        return await act(acts, CancellationToken.None);
    }
}
