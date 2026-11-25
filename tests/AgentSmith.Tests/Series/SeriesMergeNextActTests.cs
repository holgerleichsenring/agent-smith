using System.Security.Claims;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Series;
using AgentSmith.Application.Services.TicketActs;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Server.Services.Series;
using AgentSmith.Tests.Tickets;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Tickets;
using Microsoft.Extensions.Logging.Abstractions;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AgentSmith.Tests.Series;

/// <summary>2026-11-23-31dce: POST /api/series-runs/{id}/tickets/{ticketId}/merge over a real migrated store — the ready
/// ticket's open pull requests go to a merge-green run asked by the caller and its outcome comes back; a held lease and a run
/// that ended first say so. 2026-11-24-a4e7: a merged or armed pull request has the ticket's records read again before the
/// answer. 2026-11-25-9992e: the merge is judged — a ticket that is not next merges too, the merge order refuses nothing.</summary>
public sealed class SeriesMergeNextActTests : IDisposable
{
    private static readonly DateTimeOffset T = TicketStoreHarness.T;
    private readonly Mock<IMergeGreenRun> _merges = new();
    private readonly Mock<IPrRecordRefreshNow> _refresh = new();
    private readonly List<CiGreenMergeRequest> _asked = [];
    private readonly TicketStoreHarness _h;
    private IReadOnlyList<PullRequestMergeLine>? _lines = [new("api", PullRequestCompletionOutcome.Merged, "api: merged (https://github.com/o/api/pull/1)")];
    private bool _claimed = true;

    public SeriesMergeNextActTests()
    {
        _h = new(services =>
        {
            services.AddSingleton<ISeriesRunStore, SeriesRunRepository>().AddTransient<SeriesRunOrder>();
            services.AddTransient<SeriesRunPullRequests>().AddTransient<SeriesRunTickets>();
            services.AddScoped<SeriesRunProjectReads>().AddScoped<SeriesRunNames>().AddScoped<SeriesRunCosts>()
                .AddScoped<SeriesRunDetailReader>().AddScoped<SeriesMergeNextAct>().AddSingleton(_merges.Object)
                .AddSingleton(_refresh.Object).AddScoped<SeriesParkedQuestion>().AddScoped<SeriesStatusReader>() /* 2026-11-25-9992a */;
            services.AddTransient<SeriesRunHolds>(); // 31dcg: the detail reads the holds
        });
        _h.Config.Projects["alpha"] = new()
        {
            Name = "alpha", Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
            Repos = [new RepoConnection { Name = "api", DefaultBranch = "main" }, new RepoConnection { Name = "web", DefaultBranch = "main" }],
        };
        _merges.Setup(m => m.RunAsync(It.IsAny<ResolvedProject>(), It.IsAny<CiGreenMergeRequest>(), It.IsAny<CancellationToken>(),
                It.IsAny<ActSlot<IReadOnlyList<PullRequestMergeLine>>?>()))
            .Callback<ResolvedProject, CiGreenMergeRequest, CancellationToken, ActSlot<IReadOnlyList<PullRequestMergeLine>>?>((_, r, _, slot) =>
            {
                _asked.Add(r);
                if (_claimed && _lines is not null) slot!.Set(_lines);
            })
            .ReturnsAsync(() => _claimed);
    }

    [Fact]
    public async Task Merge_TheReadyTicket_RunsAMergeGreenRunAskedByTheCaller_OverItsOpenPullRequestsOnly()
    {
        await SeedAsync();
        var closed = Record("1", "web", 91);
        closed.State = PrReviewStates.Closed; // an earlier attempt's
        await _h.AddAsync(closed);

        var result = await MergeAsync("1");

        var answer = (SeriesMergeAnswer)((Ok<object>)result).Value!;
        answer.PullRequests.Should().Equal(new SeriesMergeOutcomeView("api", "merged", "api: merged (https://github.com/o/api/pull/1)"));
        var asked = _asked.Should().ContainSingle().Subject;
        asked.AskedBy.Should().Be("Dana");
        (asked.Judged, asked.Protection).Should().Be((true, (BranchProtection?)null), "the person judged it; the policy's protection holds");
        asked.ChecksTarget.Should().BeFalse("2026-11-25-96aeb: no order check holds a person's merge");
        (asked.TicketId, asked.AttemptRunId, asked.Branch).Should().Be(("1", "run-1", "agent-smith/1"));
        asked.PullRequests.Should().Equal(new CiGreenPullRequest("api", "https://github.com/o/api/pull/1"));
    }

    [Theory]
    [InlineData(PullRequestCompletionOutcome.Merged, 1)]
    [InlineData(PullRequestCompletionOutcome.Armed, 1)]
    [InlineData(PullRequestCompletionOutcome.Refused, 0)]
    public async Task Merge_AMergedOrArmedPullRequest_RefreshesTheTicketsRecordsBeforeTheAnswer(PullRequestCompletionOutcome outcome, int reads)
    {
        await SeedAsync();
        _lines = [new("api", outcome, "api: said")];

        var result = await MergeAsync("1");

        result.Should().BeOfType<Ok<object>>();
        _refresh.Verify(r => r.RefreshAsync(It.Is<ResolvedProject>(p => p.Name == "alpha"), "1"), Times.Exactly(reads));
    }

    [Fact]
    public async Task Merge_ARefreshWhoseLockThrows_StillAnswersTheMergeLines()
    {
        await SeedAsync();
        using var refreshes = new AgentSmith.Tests.Rework.Records.PrReviewRefreshHarness();
        var locks = new Mock<IRedisClaimLock>();
        locks.Setup(l => l.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("redis is gone"));
        var now = new PrRecordRefreshNow(refreshes.Refresh(), locks.Object, Mock.Of<IReworkNudges>(), NullLogger<PrRecordRefreshNow>.Instance);
        await using var scope = _h.Services.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var act = new SeriesMergeNextAct(sp.GetRequiredService<ISeriesRunStore>(), sp.GetRequiredService<TicketProjects>(),
            sp.GetRequiredService<PrReviewRecordRepository>(), sp.GetRequiredService<IPreviousAttemptReader>(), _merges.Object, now);

        var result = await SeriesRunEndpoints.MergeAsync("sr-1", "1", new DefaultHttpContext(), act, default);

        ((SeriesMergeAnswer)((Ok<object>)result).Value!).PullRequests.Should().ContainSingle().Which.Outcome.Should().Be("merged");
    }

    [Fact]
    public async Task Merge_ATicketThatIsNotNext_IsMergedJudged()
    {
        await SeedAsync();

        (await MergeAsync("2")).Should().BeOfType<Ok<object>>();

        _asked.Should().ContainSingle().Which.Should().Match<CiGreenMergeRequest>(r => r.TicketId == "2" && r.Judged);
    }

    [Fact]
    public async Task Merge_ALeaseAnotherRunHolds_SaysSo()
    {
        await SeedAsync();
        _claimed = false;

        Said(await MergeAsync("1")).Should().Be((StatusCodes.Status409Conflict, "SCRUM-1 was not merged: another run holds this ticket."));
    }

    [Fact]
    public async Task Merge_ARunThatEndedBeforeItsStep_SaysSo()
    {
        await SeedAsync();
        _lines = null;

        Said(await MergeAsync("1")).Status.Should().Be(StatusCodes.Status500InternalServerError);
    }

    [Fact]
    public async Task Merge_ATicketWithNoOpenPullRequest_IsRefused()
    {
        await SeedAsync();

        Said(await MergeAsync("3")).Should().Be((StatusCodes.Status409Conflict, "SCRUM-3 has no open pull request to merge."));
        Said(await MergeAsync("9")).Status.Should().Be(StatusCodes.Status404NotFound);
    }

    public void Dispose() => _h.Dispose();

    // 1 ready, 2 after 1, 3 never opened a pull request.
    private async Task SeedAsync()
    {
        await _h.Services.GetRequiredService<ISeriesRunStore>().OpenAsync(new SeriesRunRecord("sr-1", "conv-sr-1", "alpha", "operator", true,
            SeriesRunStates.Running, [.. new[] { "1", "2", "3" }.Select(id => new SeriesTicket(id, $"SCRUM-{id}", $"Ticket {id}", false, T))], T), default);
        await _h.AddAsync(Record("1", "api", 1), Record("2", "api", 2), TicketStoreHarness.Run("run-1", ticket: "1"),
            TicketStoreHarness.Run("run-2", ticket: "2"), new TicketRequirement
            {
                Tracker = "gh", TicketKey = "github-2", TicketId = "2", Project = "alpha",
                RequiredTicketKey = "github-1", RequiredTicketId = "1", FiledInConversation = "conv",
            });
    }

    private async Task<IResult> MergeAsync(string ticketId)
    {
        await using var scope = _h.Services.CreateAsyncScope();
        var ctx = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("name", "Dana")], "test")) };
        return await SeriesRunEndpoints.MergeAsync("sr-1", ticketId, ctx, scope.ServiceProvider.GetRequiredService<SeriesMergeNextAct>(), default);
    }

    private static (int Status, string Body) Said(IResult result) => result is ContentHttpResult content
        ? (content.StatusCode ?? StatusCodes.Status200OK, content.ResponseContent ?? string.Empty)
        : (StatusCodes.Status200OK, string.Empty);

    private static PrReviewRecord Record(string ticket, string repo, int number)
    {
        var url = $"https://github.com/o/{repo}/pull/{number}";
        return new PrReviewRecord
        {
            PrKey = PrKey.Of(url), Url = url, Repo = repo, RepoKey = RepoKey.Of($"https://github.com/o/{repo}"), Number = number.ToString(),
            Project = "alpha", TicketId = ticket, RunId = $"run-{ticket}", State = PrReviewStates.Open, HeadSha = "abc",
            CheckedAtTicks = T.UtcTicks, CiState = CiStates.Green, CiHeadSha = "abc", CiFailuresJson = PrRecordCiJson.Failures([]),
        };
    }
}
