using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Sweep;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Server.Services.Sweep;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace AgentSmith.Tests.Rework.PrSweep;

/// <summary>
/// 2026-10-15-114fc: polling mode's head comparison — an open record the sweep lists with another head, or with
/// no stored head yet, gets a late Refresh nudge with its URL; the same head gets none.
/// </summary>
public sealed class PrSweepRecordHeadTests : IDisposable
{
    private const string Url = "https://github.com/o/r/pull/4";
    private static readonly RepoConnection Repo = new() { Name = "r", Url = "https://github.com/o/r", Type = RepoType.GitHub };
    private static readonly DateTimeOffset Noon = new(2026, 10, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly ServerStateStore _store = new();
    private readonly List<ReworkNudgeRequest> _nudged = [];

    public void Dispose() => _store.Dispose();

    private async Task RecordAsync(string? head)
    {
        await using var ctx = _store.Context();
        ctx.Add(new PrReviewRecord
        {
            PrKey = PrKey.Of(Url), Url = Url, Repo = "r", RepoKey = RepoKey.Of(Repo.Url!), Number = "4",
            Project = "p", TicketId = "12", RunId = "run-1", State = PrReviewStates.Open, HeadSha = head,
        });
        await ctx.SaveChangesAsync();
    }

    private async Task SweepAsync(string listedHead)
    {
        var nudges = new Mock<IReworkNudges>();
        nudges.Setup(n => n.EnqueueAsync(It.IsAny<ReworkNudgeRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ReworkNudgeRequest, CancellationToken>((r, _) => _nudged.Add(r)).Returns(Task.CompletedTask);
        using var scope = _store.ScopeFactory.CreateScope();
        var page = new OpenPullRequestsPage([new OpenPullRequest("4", Url, listedHead, "base", "feature", "alice", [], Noon, Noon)]);
        await new PrSweepRecordNudge(scope.ServiceProvider.GetRequiredService<PrReviewRecordRepository>(), nudges.Object)
            .RunAsync(new SweepTarget(new AgentSmithConfig(), Repo, new HashSet<string> { "p" }), RepoKey.Of(Repo.Url!), page, null, CancellationToken.None);
    }

    [Fact]
    public async Task PrSweepRecordNudge_ListedHeadDiffers_NudgesARefresh()
    {
        await RecordAsync("h1");

        await SweepAsync("h2");

        _nudged.Should().ContainSingle().Which.Should().Match<ReworkNudgeRequest>(n =>
            n.Project == "p" && n.TicketId == "12" && n.PrUrl == Url && n.Origin == ReworkNudgeOrigin.Refresh && n.IsLate);
    }

    [Fact]
    public async Task PrSweepRecordNudge_SameHead_DoesNotNudge()
    {
        await RecordAsync("h1");

        await SweepAsync("h1");

        _nudged.Should().BeEmpty();
    }

    [Fact]
    public async Task PrSweepRecordNudge_NoStoredSha_NudgesOnceAsBackfill()
    {
        await RecordAsync(null);

        await SweepAsync("h1");
        await using (var ctx = _store.Context())
        {
            ctx.Set<PrReviewRecord>().Single().HeadSha = "h1";
            await ctx.SaveChangesAsync();
        }
        await SweepAsync("h1");

        _nudged.Should().ContainSingle("the refresh the first nudge asks for stores the head, and the same head nudges no more");
    }
}
