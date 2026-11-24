using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Tickets.Holds;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using static AgentSmith.Tests.Rework.Records.PrReviewRefreshHarness;

namespace AgentSmith.Tests.Rework.Records;

/// <summary>2026-11-24-9dc0e: Try again on a refused refresh starts the same refresh-base run for that pull request — once,
/// only while the refusal stands, and only for a free ticket.</summary>
public sealed class BaseRefreshRetryTests : IDisposable
{
    private readonly PrReviewRefreshHarness _h = new();

    public BaseRefreshRetryTests() =>
        _h.Provider.Setup(p => p.ReadPullRequestBaseAsync(Pr1, It.IsAny<CancellationToken>())).ReturnsAsync("main");

    public void Dispose() => _h.Dispose();

    [Fact]
    public async Task TryAgain_AStandingRefusal_LaunchesOneRun_AndASecondClickIsRefused()
    {
        await RecordAsync();
        await RefusedRefreshAsync();

        var first = await _h.Retry().TryAgainAsync(Project, "7", Pr1, CancellationToken.None);
        var second = await _h.Retry().TryAgainAsync(Project, "7", Pr1, CancellationToken.None);

        first.RunId.Should().NotBeNull(first.Refusal);
        var rows = _h.Store.Context().Set<RunRepo>().AsNoTracking().Where(r => r.RunId == first.RunId).ToList();
        rows.Single().PrUrl.Should().Be(Pr1);
        second.Refusal.Should().Contain("already at work");
        _h.Queue.Verify(q => q.EnqueueAsync(It.Is<PipelineRequest>(r => r.PipelineName == PipelinePresets.RefreshBaseName),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task TryAgain_ATicketAtWork_IsRefused()
    {
        await RecordAsync();
        await RefusedRefreshAsync();
        _h.Leases.Setup(l => l.GetByTicketAsync("p", new TicketId("7"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new StaleLease("p", new TicketId("7"), "run-7", null, DateTimeOffset.UtcNow));

        var answer = await _h.Retry().TryAgainAsync(Project, "7", Pr1, CancellationToken.None);

        answer.Refusal.Should().Contain("not free");
    }

    [Fact]
    public async Task TryAgain_NoRefusal_OrOneAHeadMoveOvertook_IsRefused()
    {
        await RecordAsync();
        (await _h.Retry().TryAgainAsync(Project, "7", Pr1, CancellationToken.None)).Refusal.Should().Contain("No refused refresh");

        await RefusedRefreshAsync();
        await using (var db = _h.Store.Context())
        {
            db.Set<PrReviewRecord>().Single().HeadMovedAtTicks = T.AddHours(5).UtcTicks;
            await db.SaveChangesAsync();
        }
        (await _h.Retry().TryAgainAsync(Project, "7", Pr1, CancellationToken.None)).Refusal.Should().Contain("No refused refresh");
        (await _h.Retry().TryAgainAsync(Project, "7", Pr2, CancellationToken.None)).Refusal.Should().Contain("no open pull request");
    }

    [Fact]
    public async Task TheRefusedHold_OpensTheRefusedPullRequest()
    {
        await RecordAsync();
        await RefusedRefreshAsync();
        var views = await _h.Store.Services.CreateScope().ServiceProvider.GetRequiredService<PrReviewRecordRepository>()
            .ByTicketAsync("p", "7", CancellationToken.None);
        var said = await _h.Store.Services.CreateScope().ServiceProvider.GetRequiredService<PullRequestMergeSaidReader>()
            .RefreshesAsync([Pr1], CancellationToken.None);

        TicketOpenPullRequestsReader.Of(views, said).Refresh!.Url.Should().Be(Pr1);
    }

    private async Task RecordAsync()
    {
        await _h.RunAsync("r1", Pr1);
        _h.Provider.As<IPrReviewStateReader>().Setup(s => s.ReadAsync(Pr1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PrReviewState("1", PrReviewStates.Open, T, [], "a") { HasConflicts = false });
        await _h.RefreshAsync();
    }

    private async Task RefusedRefreshAsync()
    {
        await using var db = _h.Store.Context();
        db.Add(new Run { Id = "refresh-1", Project = "p", TicketId = string.Empty, Pipeline = PipelinePresets.RefreshBaseName,
            Status = RunStatuses.Success, StartedAt = T.AddMinutes(30) });
        db.Add(new RunRepo { RunId = "refresh-1", RepoName = "api", PrUrl = Pr1, PrStatus = PullRequestStatuses.RefreshRefused,
            Reason = "the old harness lost it" });
        await db.SaveChangesAsync();
    }
}
