using AgentSmith.Application.Services;
using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Delivery;

/// <summary>
/// 2026-10-25-0099b: the delivery reads over a real migrated store — the code ticket runs whose id falls in the
/// window, in the caller's projects before the ceiling, with the act that started each; and what they worked on.
/// </summary>
public sealed class DeliveryRepositoryTests : IDisposable
{
    private static readonly DateTimeOffset T = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
    private static readonly string From = RunIdGenerator.TimestampOf(T);
    private static readonly string To = RunIdGenerator.TimestampOf(T.AddDays(1));
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task DeliveryAttempts_Window_ReadsOnlyCodeTicketRunsWhoseIdFallsInIt()
    {
        var inside = Seed("alpha", T.AddHours(1));
        Seed("alpha", T.AddHours(-1));
        Seed("alpha", T.AddDays(1));
        Seed("alpha", T.AddHours(2), pipeline: "security-scan");
        Seed("alpha", T.AddHours(3), ticket: "");

        var read = await ReadAsync(ProjectScope.Everything);

        read.Rows.Select(r => r.RunId).Should().Equal(inside);
        read.Truncated.Should().BeFalse();
    }

    [Fact]
    public async Task DeliveryAttempts_ScopedCaller_SeesOnlyTheirProjectsBeforeTheCeiling()
    {
        var alpha = Seed("alpha", T.AddHours(1));
        for (var i = 0; i < 3; i++) Seed("beta", T.AddHours(2 + i));

        var read = await ReadAsync(ProjectScope.Of(["alpha"]), ceiling: 2);

        read.Rows.Select(r => r.RunId).Should().Equal([alpha], "beta's newer runs are not the caller's ceiling");
        read.Truncated.Should().BeFalse();
        (await ReadAsync(ProjectScope.Everything, ceiling: 2)).Truncated.Should().BeTrue();
    }

    [Fact]
    public async Task DeliveryAttempts_CarryTheirCauseChannel_AndNoneWithoutAnAttemptRow()
    {
        var reworked = Seed("alpha", T.AddHours(1), cause: "pull_request");
        var early = Seed("alpha", T.AddHours(2));

        var rows = (await ReadAsync(ProjectScope.Everything)).Rows.ToDictionary(r => r.RunId);

        rows[reworked].CauseChannel.Should().Be("pull_request");
        rows[early].CauseChannel.Should().BeNull("a run that died before fetching its ticket has no attempt row");
    }

    [Fact]
    public void DeliveryAttempts_SqlServer_TranslatesTheWindowAndTheLeftJoin()
    {
        var options = new DbContextOptionsBuilder<AgentSmithDbContext>()
            .UseSqlServer("Server=localhost;Database=x;TrustServerCertificate=True").Options;
        using var ctx = new AgentSmithDbContext(options);

        var sql = (from run in ctx.Runs.AsNoTracking().InWindow(ProjectScope.Of(["alpha", "beta"]), From, To)
                   join attempt in ctx.Set<RunAttempt>() on run.Id equals attempt.RunId into causes
                   from attempt in causes.DefaultIfEmpty()
                   select new { run.Id, attempt!.CauseChannel }).ToQueryString();

        sql.Should().Contain("LEFT JOIN").And.Contain("[r].[Id] >= @");
    }

    [Fact]
    public async Task DeliveryWork_SpecsAndChangedRepos_OfWindowRunsOnly()
    {
        var inside = Seed("alpha", T.AddHours(1));
        var outside = Seed("alpha", T.AddDays(2));
        await using (var ctx = Context())
        {
            ctx.RunSpecs.AddRange(Spec(inside, 1, "done"), Spec(inside, 2, "failed"), Spec(outside, 1, "done"));
            ctx.RunRepos.AddRange(Repo(inside, "api", "https://example.com/pr/1"), Repo(inside, "docs", null),
                Repo(outside, "web", "https://example.com/pr/2"));
            await ctx.SaveChangesAsync();
        }
        await using var read = Context();
        var work = new DeliveryWorkRepository(read);

        var specs = await work.SpecsAsync(ProjectScope.Everything, From, To, CancellationToken.None);
        var repos = await work.ReposAsync(ProjectScope.Everything, From, To, CancellationToken.None);

        specs.Select(s => (s.RunId, s.Ordinal, s.Status)).Should().BeEquivalentTo([(inside, 1, "done"), (inside, 2, "failed")]);
        repos.Should().Equal(new DeliveryRepoRow(inside, "api"));
    }

    /// <summary>
    /// 2026-10-26-cb25f: the repositories as the projections write them — the run start names three, the commit/PR
    /// step reports one opened, one without changes, one failed, a spec-only draft opened, and one opened then failed
    /// on a retry. Before, the read filtered on ChangeCount, which nothing writes, and listed none of them.
    /// </summary>
    [Fact]
    public async Task DeliveryWork_ProjectedRuns_ListTheRepositoriesWithAPullRequest()
    {
        var id = RunIdGenerator.Generate(T.AddHours(1));
        string[] repos = ["api", "docs", "web", "specs", "retried"];
        await ApplyAsync(new RunStartedEvent(id, "ticket", "code", repos, T.AddHours(1), TicketId: "T-1", Project: "alpha"));
        await ApplyAsync(Outcome(id, "api", "opened", "https://example.com/api/pull/1"));
        await ApplyAsync(Outcome(id, "docs", "no_changes", null));
        await ApplyAsync(Outcome(id, "web", "failed", null, "push refused"));
        await ApplyAsync(Outcome(id, "specs", "opened", "https://example.com/specs/pull/2"));
        await ApplyAsync(Outcome(id, "retried", "opened", "https://example.com/retried/pull/3"));
        await ApplyAsync(Outcome(id, "retried", "failed", null, "the retry failed"));
        await using var read = Context();

        var changed = await new DeliveryWorkRepository(read).ReposAsync(ProjectScope.Everything, From, To, CancellationToken.None);

        changed.Select(r => r.RepoName).Should().BeEquivalentTo(["api", "specs"],
            "a repository a run changed carries its pull request; the last outcome per repository is the run's record");
    }

    private async Task ApplyAsync(AgentSmith.Contracts.Events.RunEvent e)
    {
        await using var ctx = Context();
        await RunEventAppliers.Default().ApplyAsync(ctx, e, CancellationToken.None);
    }

    private static PullRequestOutcomeEvent Outcome(string runId, string repo, string status, string? url, string? reason = null) =>
        new(runId, repo, status, T.AddHours(1), url, reason);

    private async Task<DeliveryAttemptRead> ReadAsync(ProjectScope scope, int ceiling = 100)
    {
        await using var ctx = Context();
        return await new DeliveryAttemptRepository(ctx).ReadAsync(scope, From, To, ceiling, CancellationToken.None);
    }

    private string Seed(string project, DateTimeOffset at, string pipeline = "code", string ticket = "T-1", string? cause = null)
    {
        var id = RunIdGenerator.Generate(at);
        using var ctx = Context();
        ctx.Runs.Add(new Run { Id = id, Project = project, Pipeline = pipeline, TicketId = ticket, Status = "success", StartedAt = at, FinishedAt = at.AddMinutes(5) });
        if (cause is not null) ctx.Set<RunAttempt>().Add(new RunAttempt { RunId = id, CauseChannel = cause });
        ctx.SaveChanges();
        return id;
    }

    private static RunSpec Spec(string runId, int ordinal, string status) =>
        new() { RunId = runId, SpecId = $"s{ordinal}", Ordinal = ordinal, Title = $"goal {ordinal}", Status = status };

    private static RunRepo Repo(string runId, string name, string? prUrl) => new() { RunId = runId, RepoName = name, PrUrl = prUrl };

    private AgentSmithDbContext Context() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
}
