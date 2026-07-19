using System.Text.Json;
using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services.Events;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0347: agent-smith's PR OUTPUT is real, durable, multi-repo-complete data end
/// to end — PullRequestOutcomeEvent is projected onto Runs.PullRequestsJson by the
/// applier (upsert by repo) and flattened across runs by GET /api/pull-requests.
/// Pre-p0347 rows fall back to the run's lone PR url so history isn't blank.
/// p0350 reconciliation: the run snapshot's PR list is now the crash-resilient
/// run.Repos-based surfacing (every OPENED PR, 4-field repo/url/status/isDraft);
/// the durable PullRequestsJson projection + Flatten page below are unchanged.
/// </summary>
public sealed class PullRequestsServedTests : IDisposable
{
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-07-17T10:00:00Z");
    private readonly SqliteConnection _connection;

    public PullRequestsServedTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Database.Migrate();
    }

    public void Dispose() => _connection.Dispose();

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    private RunRepository NewStore() => new(new AgentSmithDbContext(Options()));

    // p0347 spec test: PullRequests_ProjectedFromOutcomeEvents_PersistAndServe
    [Fact]
    public async Task PullRequests_ProjectedFromOutcomeEvents_PersistAndServe()
    {
        const string runId = "2026-07-17T10-00-00-0001";
        await ApplyAsync(
            new RunStartedEvent(runId, "ticket", "fix-bug", ["api"], T, "claude", "42"),
            new TicketFetchedEvent(runId, "42", "Fix the login bug", "desc", "Open", [], 0, "github", T),
            new PullRequestOutcomeEvent(runId, "api", "opened", T.AddMinutes(3), "https://git/pr/1"),
            new RunFinishedEvent(runId, "success", "https://git/pr/1", "done", T.AddMinutes(5)));

        // Persisted on the run row (survives a fresh context = process restart).
        var run = await NewStore().GetRunDetailAsync(runId, CancellationToken.None);
        run!.PullRequestsJson.Should().NotBeNull();

        // Served by GET /api/pull-requests — flattened + joined to run/ticket facts.
        var list = PullRequestQueryEndpoints.Flatten([run]);
        list.Should().ContainSingle();
        list[0].Should().Be(new AgentSmith.Contracts.Runs.PullRequestListItem(
            runId, "42", "Fix the login bug", "fix-bug",
            "api", "opened", "https://git/pr/1", null, T.AddMinutes(3)));

        // p0350: the run DETAIL snapshot surfaces every OPENED PR from run.Repos in
        // the 4-field wire shape (repo/url/status/isDraft); a success run is no draft.
        var snap = RunSnapshotMapper.ToSnapshot(run, includeStory: true);
        snap.PullRequests.Should().ContainSingle();
        snap.PullRequests![0].Should().Be(new RunPullRequestView("api", "https://git/pr/1", "opened", false));
        var wire = JsonSerializer.Serialize(snap, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        wire.Should().Contain(
            "\"pullRequests\":[{\"repo\":\"api\",\"url\":\"https://git/pr/1\",\"status\":\"opened\",\"isDraft\":false}]");
    }

    // p0347 spec test: PullRequests_MultiRepoRun_KeepsEveryRepoPr
    [Fact]
    public async Task PullRequests_MultiRepoRun_KeepsEveryRepoPr()
    {
        const string runId = "2026-07-17T10-00-00-0002";
        await ApplyAsync(
            new RunStartedEvent(runId, "ticket", "feature", ["api", "web", "docs"], T, "claude", "7"),
            new PullRequestOutcomeEvent(runId, "api", "opened", T.AddMinutes(1), "https://git/pr/api"),
            new PullRequestOutcomeEvent(runId, "web", "opened", T.AddMinutes(2), "https://git/pr/web"),
            new PullRequestOutcomeEvent(runId, "docs", "no_changes", T.AddMinutes(3), null, "nothing to commit"),
            new RunFinishedEvent(runId, "success", "https://git/pr/api", "done", T.AddMinutes(5)));

        var run = await NewStore().GetRunDetailAsync(runId, CancellationToken.None);
        var snap = RunSnapshotMapper.ToSnapshot(run!, includeStory: true);

        // p0350: the snapshot surfaces only the OPENED PRs (docs opened none —
        // no_changes), each in the 4-field shape. The Flatten page keeps every repo.
        snap.PullRequests.Should().HaveCount(2);
        snap.PullRequests!.Select(p => p.Repo).Should().BeEquivalentTo(["api", "web"]);
        snap.PullRequests.Single(p => p.Repo == "web").Url.Should().Be("https://git/pr/web");

        // The flattened list surfaces all three, newest-first by openedAt.
        var list = PullRequestQueryEndpoints.Flatten([run]);
        list.Should().HaveCount(3);
        list.Select(x => x.Repo).Should().ContainInOrder("docs", "web", "api");
    }

    [Fact]
    public async Task PullRequests_RetriedOutcome_LastPerRepoWins()
    {
        const string runId = "2026-07-17T10-00-00-0003";
        await ApplyAsync(
            new RunStartedEvent(runId, "ticket", "fix-bug", ["api"], T, "claude", "9"),
            new PullRequestOutcomeEvent(runId, "api", "failed", T.AddMinutes(1), null, "push rejected"),
            new PullRequestOutcomeEvent(runId, "api", "opened", T.AddMinutes(2), "https://git/pr/retry"),
            new RunFinishedEvent(runId, "success", "https://git/pr/retry", "done", T.AddMinutes(5)));

        var run = await NewStore().GetRunDetailAsync(runId, CancellationToken.None);
        var snap = RunSnapshotMapper.ToSnapshot(run!, includeStory: true);

        snap.PullRequests.Should().ContainSingle("the last outcome per repo wins");
        snap.PullRequests![0].Status.Should().Be("opened");
        snap.PullRequests[0].Url.Should().Be("https://git/pr/retry");
    }

    // p0347 spec test: PullRequests_PreMigrationRun_FallsBackToRunPrUrl
    [Fact]
    public async Task PullRequests_PreMigrationRun_FallsBackToRunPrUrl()
    {
        const string runId = "2026-07-17T10-00-00-0004";
        // A pre-p0347 row: PullRequestsJson is null, but the per-repo RunRepo row
        // carries the lone opened PR (the only durable trace before this migration).
        await using (var ctx = new AgentSmithDbContext(Options()))
        {
            ctx.Add(new Run
            {
                Id = runId, Pipeline = "fix-bug", TicketId = "88", TicketTitle = "Old run",
                Status = "success", StartedAt = T, FinishedAt = T.AddMinutes(5),
                PullRequestsJson = null,
            });
            ctx.Add(new RunRepo { RunId = runId, RepoName = "legacy", PrStatus = "opened", PrUrl = "https://git/pr/old" });
            await ctx.SaveChangesAsync();
        }

        var run = await NewStore().GetRunDetailAsync(runId, CancellationToken.None);

        // The flattened list contributes ONE fallback row from the lone PR url.
        var list = PullRequestQueryEndpoints.Flatten([run!]);
        list.Should().ContainSingle();
        list[0].Repo.Should().Be("legacy");
        list[0].Status.Should().Be("opened");
        list[0].Url.Should().Be("https://git/pr/old");
        list[0].OpenedAt.Should().Be(T.AddMinutes(5), "the fallback uses the run's finished time");

        // The detail snapshot serves the single fallback entry too, not null.
        var snap = RunSnapshotMapper.ToSnapshot(run!, includeStory: true);
        snap.PullRequests.Should().ContainSingle();
        snap.PullRequests![0].Url.Should().Be("https://git/pr/old");
    }

    [Fact]
    public async Task PullRequests_RunWithNoPr_ContributesNoRows_DetailNull()
    {
        const string runId = "2026-07-17T10-00-00-0005";
        await ApplyAsync(
            new RunStartedEvent(runId, "ticket", "fix-bug", ["api"], T, "claude", "1"),
            new RunFinishedEvent(runId, "failed", null, "broke before PR", T.AddMinutes(5)));

        var run = await NewStore().GetRunDetailAsync(runId, CancellationToken.None);

        PullRequestQueryEndpoints.Flatten([run!]).Should().BeEmpty("no PR was opened — an honest empty state");
        // p0350: no opened PR in run.Repos → an empty list, an honest empty state.
        RunSnapshotMapper.ToSnapshot(run!, includeStory: true).PullRequests.Should().BeEmpty();
    }

    private async Task ApplyAsync(params AgentSmith.Contracts.Events.RunEvent[] events)
    {
        var applier = new RunEventApplier();
        foreach (var ev in events)
        {
            await using var uow = new AgentSmithDbContext(Options());
            await applier.ApplyAsync(uow, ev, CancellationToken.None);
        }
    }
}
