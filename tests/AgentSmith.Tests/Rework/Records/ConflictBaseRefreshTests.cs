using AgentSmith.Application.Services.Holds;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Branches;
using AgentSmith.Server.Services.Tickets.Holds;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using static AgentSmith.Tests.Rework.Records.PrReviewRefreshHarness;

namespace AgentSmith.Tests.Rework.Records;

/// <summary>
/// 2026-11-24-297b: a pull request its host says conflicts is handed to one refresh-base run by the record read, in a
/// project without merge.refresh_open_pull_requests — once per conflict on a head, never while one is at work, never for a
/// ticket that is not free — and the queued run is there before the conflict is, so the hold says refreshing.
/// </summary>
public sealed class ConflictBaseRefreshTests : IDisposable
{
    private readonly PrReviewRefreshHarness _h = new();

    public ConflictBaseRefreshTests() =>
        _h.Provider.Setup(p => p.ReadPullRequestBaseAsync(Pr1, It.IsAny<CancellationToken>())).ReturnsAsync("main");

    public void Dispose() => _h.Dispose();

    [Fact]
    public void Due_WithoutARefresh_OrOneOlderThanTheConflict()
    {
        ConflictBaseRefresh.Due(null, T).Should().BeTrue();
        ConflictBaseRefresh.Due(new PullRequestRefreshTried("r", false, T.AddMinutes(-1)), T).Should().BeTrue();
        ConflictBaseRefresh.Due(new PullRequestRefreshTried("r", false, T), T).Should().BeFalse("it started with this conflict");
        ConflictBaseRefresh.Due(new PullRequestRefreshTried("r", true, T.AddMinutes(-1)), T).Should().BeFalse("one is at work");
    }

    [Fact]
    public void ConflictsSince_KeepsTheStampOnTheSameHead_AndRestartsOnAMove()
    {
        var stored = View() with { HeadSha = "a", HasConflicts = true, ConflictsAt = T };

        ConflictBaseRefresh.ConflictsSince(stored, "a", T.AddHours(1)).Should().Be(T);
        ConflictBaseRefresh.ConflictsSince(stored, "b", T.AddHours(1)).Should().Be(T.AddHours(1));
        ConflictBaseRefresh.ConflictsSince(stored with { HasConflicts = false, ConflictsAt = null }, "a", T.AddHours(1))
            .Should().Be(T.AddHours(1));
        ConflictBaseRefresh.ConflictsSince(null, "a", T.AddHours(1)).Should().Be(T.AddHours(1));
    }

    [Fact]
    public async Task AConflictingRead_LaunchesOneRefresh_AndTheHoldReadsItAtWork()
    {
        await _h.RunAsync("r1", Pr1);
        State(true, "a");

        await _h.RefreshAsync();

        var run = RefreshRuns().Should().ContainSingle().Subject;
        RepoRows(run.Id).Single().PrUrl.Should().Be(Pr1);
        _h.Queue.Verify(q => q.EnqueueAsync(It.Is<PipelineRequest>(r => r.PipelineName == PipelinePresets.RefreshBaseName),
            It.IsAny<CancellationToken>()), Times.Once);
        var said = await Said().RefreshesAsync([Pr1], CancellationToken.None);
        said[Pr1].InFlight.Should().BeTrue("the hold reads refreshing, not pr_conflict, from the first read on");
    }

    [Fact]
    public async Task TheRefreshIsQueuedBeforeTheConflictIsWritten_AndTheHoldSaysRefreshingNotYourMove()
    {
        await _h.RunAsync("r1", Pr1);
        State(true, "a");
        bool? recordedAtTheLaunch = false;
        _h.Queue.Setup(q => q.EnqueueAsync(It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>()))
            .Callback(() => recordedAtTheLaunch = _h.Records().SingleOrDefault()?.HasConflicts);

        await _h.RefreshAsync();

        recordedAtTheLaunch.Should().BeNull("no reader can see the conflict before its refresh is queued");
        var views = await _h.Store.Services.CreateScope().ServiceProvider.GetRequiredService<PrReviewRecordRepository>()
            .ByTicketAsync("p", "7", CancellationToken.None);
        var open = TicketOpenPullRequestsReader.Of(views, await Said().RefreshesAsync([Pr1], CancellationToken.None));
        var hold = new TicketHoldReader().Of(new TicketHoldFacts("p", "7")
        {
            HeadStatus = RunStatuses.Success, HeadRunId = "r1", Attempt = 1, EndedAt = T, OpenPullRequests = open,
        })!;
        (hold.Addressee, hold.Case).Should().Be((TicketHoldAddressees.Nobody, TicketHoldCases.Refreshing));
    }

    [Fact]
    public async Task TheSameConflictReadAgain_AfterTheRefreshEnded_LaunchesNoSecond()
    {
        await _h.RunAsync("r1", Pr1);
        State(true, "a");
        await _h.RefreshAsync();
        await EndRefreshesAsync(RunStatuses.Failed);

        _h.Clock.Now = _h.Clock.Now.AddMinutes(10);
        await _h.RefreshAsync();

        RefreshRuns().Should().ContainSingle("a refused or failed refresh stands until the head or the base moves");
    }

    [Fact]
    public async Task AConflictOnAMovedHead_IsRefreshedAgain()
    {
        await _h.RunAsync("r1", Pr1);
        State(true, "a");
        await _h.RefreshAsync();
        await EndRefreshesAsync(RunStatuses.Failed);

        _h.Clock.Now = _h.Clock.Now.AddMinutes(10);
        State(true, "b");
        await _h.RefreshAsync();

        RefreshRuns().Should().HaveCount(2);
    }

    [Fact]
    public async Task ACleanRead_OrATicketAtWork_LaunchesNothing()
    {
        await _h.RunAsync("r1", Pr1);
        State(false, "a");
        await _h.RefreshAsync();
        RefreshRuns().Should().BeEmpty();

        _h.Leases.Setup(l => l.GetByTicketAsync("p", new TicketId("7"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StaleLease("p", new TicketId("7"), "run-7", null, DateTimeOffset.UtcNow));
        _h.Clock.Now = _h.Clock.Now.AddMinutes(10);
        State(true, "a");
        await _h.RefreshAsync();
        RefreshRuns().Should().BeEmpty("a run of the ticket's own merges the base at its checkout");
        _h.Records().Single().HasConflicts.Should().BeTrue("the conflict is still recorded");
    }

    [Fact]
    public async Task NewestRefreshes_AnswersASuccessfulRunToo()
    {
        await using (var db = _h.Store.Context())
        {
            db.Add(new Run { Id = "old", Project = "p", TicketId = string.Empty, Pipeline = PipelinePresets.RefreshBaseName, Status = RunStatuses.Failed, StartedAt = T });
            db.Add(new RunRepo { RunId = "old", RepoName = "api", PrUrl = Pr1 });
            db.Add(new Run { Id = "new", Project = "p", TicketId = string.Empty, Pipeline = PipelinePresets.RefreshBaseName, Status = RunStatuses.Success, StartedAt = T.AddHours(1) });
            db.Add(new RunRepo { RunId = "new", RepoName = "api", PrUrl = Pr1 });
            await db.SaveChangesAsync();
        }

        var tried = await Said().NewestRefreshesAsync([Pr1], CancellationToken.None);

        tried[Pr1].Should().Be(new PullRequestRefreshTried("new", false, T.AddHours(1)));
        (await Said().RefreshesAsync([Pr1], CancellationToken.None)).Should().BeEmpty("a refresh that succeeded holds nothing");
    }

    private PullRequestMergeSaidReader Said() =>
        _h.Store.Services.CreateScope().ServiceProvider.GetRequiredService<PullRequestMergeSaidReader>();

    private List<Run> RefreshRuns() => [.. _h.Store.Context().Set<Run>().AsNoTracking()
        .Where(r => r.Pipeline == PipelinePresets.RefreshBaseName)];

    private List<RunRepo> RepoRows(string runId) => [.. _h.Store.Context().Set<RunRepo>().AsNoTracking().Where(r => r.RunId == runId)];

    private async Task EndRefreshesAsync(string status)
    {
        await using var db = _h.Store.Context();
        foreach (var run in db.Set<Run>().Where(r => r.Pipeline == PipelinePresets.RefreshBaseName)) run.Status = status;
        await db.SaveChangesAsync();
    }

    private void State(bool? conflicts, string head) =>
        _h.Provider.As<IPrReviewStateReader>().Setup(s => s.ReadAsync(Pr1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PrReviewState("1", PrReviewStates.Open, T, [], head) { HasConflicts = conflicts });

    private static PrReviewRecordView View() =>
        new(PrKey.Of(Pr1), Pr1, "api", "1", "p", "7", "r1", PrReviewStates.Open, T, T, null, []);
}
