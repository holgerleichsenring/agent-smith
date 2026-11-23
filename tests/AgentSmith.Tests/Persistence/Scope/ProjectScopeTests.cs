using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence.Scope;

/// <summary>
/// 2026-10-15-4a4dd: the projects a read may hold — everything leaves a query as it was, one name is today's
/// equality, several a collection; a conversation without a project stays listed.
/// </summary>
public sealed class ProjectScopeTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public void ProjectScope_Narrowed_EverythingToSingle_ScopedToSingleOrNone()
    {
        ProjectScope.Everything.Narrowed(null).Should().BeSameAs(ProjectScope.Everything);
        ProjectScope.Everything.Narrowed("Beta").Only.Should().Equal("Beta");
        var scoped = ProjectScope.Of(["alpha", "team/alpha"]);
        scoped.Narrowed("").Should().BeSameAs(scoped);
        scoped.Narrowed("ALPHA").Only.Should().Equal(["alpha"], "the configured spelling is what the store holds");
        scoped.Narrowed("beta").Only.Should().BeEmpty();
    }

    // 2026-10-25-0099b: several chips — the union of each narrowed alone; one admitted name stays today's equality.
    [Fact]
    public void ProjectScope_NarrowedTo_UnionOfAdmittedNames_UnknownReadsAsNone()
    {
        var scoped = ProjectScope.Of(["alpha", "team/alpha"]);

        scoped.NarrowedTo([]).Should().BeSameAs(scoped);
        scoped.NarrowedTo(["ALPHA", "beta"]).Only.Should().Equal(["alpha"]);
        scoped.NarrowedTo(["alpha", "team/alpha"]).Only.Should().Equal(["alpha", "team/alpha"]);
        scoped.NarrowedTo(["beta"]).Only.Should().BeEmpty();
        ProjectScope.Everything.NarrowedTo(["beta", "gamma"]).Only.Should().Equal(["beta", "gamma"]);
    }

    [Fact]
    public void ProjectScope_Admits_ComparesUnderConfigNames()
    {
        ProjectScope.Everything.Admits("anything").Should().BeTrue();
        ProjectScope.Of(["alpha"]).Admits("Alpha").Should().BeTrue();
        ProjectScope.Of(["alpha"]).Admits("beta").Should().BeFalse();
        ProjectScope.Of([]).Admits("alpha").Should().BeFalse();
    }

    [Fact]
    public void ProjectScopeQueries_Everything_ReturnsTheSameQueryInstance()
    {
        using var ctx = Context();
        var runs = ctx.Runs.AsNoTracking();

        runs.Within(ProjectScope.Everything).Should().BeSameAs(runs);
    }

    [Fact]
    public void ProjectScopeQueries_OneName_EmitsTodaysEqualityPredicate()
    {
        using var ctx = Context();
        var project = "alpha";
        var today = ctx.Runs.AsNoTracking().Where(r => r.Project == project).ToQueryString();

        ctx.Runs.AsNoTracking().Within(ProjectScope.Single("alpha")).ToQueryString().Should().Be(today);
    }

    [Fact]
    public async Task ProjectScopeQueries_Several_KeepOnlyThoseProjects()
    {
        await SeedRunsAsync("alpha", "beta", "gamma");
        await using var ctx = Context();

        var projects = await ctx.Runs.AsNoTracking().Within(ProjectScope.Of(["alpha", "gamma"]))
            .Select(r => r.Project).ToListAsync();

        projects.Should().BeEquivalentTo(["alpha", "gamma"]);
    }

    [Fact]
    public async Task ProjectScopeQueries_Scoped_SessionWithoutProjectStaysListed()
    {
        await using (var seed = Context())
        {
            seed.Add(Session("s-none", ""));
            seed.Add(Session("s-alpha", "alpha"));
            seed.Add(Session("s-beta", "beta"));
            await seed.SaveChangesAsync();
        }
        await using var ctx = Context();

        var listed = await ctx.Set<SpecDialogSession>().Within(ProjectScope.Of(["alpha"]))
            .Select(s => s.SessionId).ToListAsync();

        listed.Should().BeEquivalentTo(["s-none", "s-alpha"]);
    }

    // 2026-11-23-47b6: the rule a run reads is the rule a pull-request record and a filed series read.
    [Fact]
    public async Task ProjectScopeQueries_EverythingBut_HidesThatProjectsRunsRecordsAndFilings()
    {
        await SeedRunsAsync("alpha", InstallationScope.Name);
        await SeedRecordsAsync("alpha", InstallationScope.Name);
        await SeedFilingsAsync("alpha", InstallationScope.Name);
        await using var ctx = Context();
        var hiding = ProjectScope.EverythingBut(InstallationScope.Name);

        (await ctx.Runs.AsNoTracking().Within(hiding).Select(r => r.Project).ToListAsync()).Should().Equal("alpha");
        (await ctx.Set<PrReviewRecord>().AsNoTracking().Within(hiding).Select(r => r.Project).ToListAsync()).Should().Equal("alpha");
        (await ctx.Set<ApprovedSeries>().AsNoTracking().Within(hiding).Select(a => a.Project).ToListAsync()).Should().Equal("alpha");
        (await ctx.Set<PrReviewRecord>().AsNoTracking().Within(ProjectScope.Everything).CountAsync()).Should().Be(2);
    }

    [Fact]
    public void ProjectScopeQueries_PrRecords_EverythingIsTheQueryAndOneNameTodaysEquality()
    {
        using var ctx = Context();
        var records = ctx.Set<PrReviewRecord>().AsNoTracking();
        var project = "alpha";
        var today = records.Where(r => r.Project == project).ToQueryString();

        records.Within(ProjectScope.Everything).Should().BeSameAs(records);
        records.Within(ProjectScope.Single("alpha")).ToQueryString().Should().Be(today);
    }

    // 2026-11-23-47b6: a background read names its projects and takes no scope — the installation's record stays in it.
    [Fact]
    public async Task PrReviewRecordRepository_BackgroundRead_StillReadsAHiddenProjectsRecord()
    {
        await SeedRecordsAsync(InstallationScope.Name);
        await using var ctx = Context();

        var unread = await new PrReviewRecordRepository(ctx).OpenUncheckedAsync(
            [], [InstallationScope.Name], long.MaxValue, 10, CancellationToken.None);

        unread.Select(r => r.Project).Should().Equal(InstallationScope.Name);
    }

    private async Task SeedRecordsAsync(params string[] projects)
    {
        await using var ctx = Context();
        foreach (var project in projects)
        {
            var url = $"https://github.com/o/{project.TrimStart('@')}/pull/1";
            ctx.Add(new PrReviewRecord
            {
                PrKey = PrKey.Of(url), Url = url, Repo = "api", Number = "1", Project = project, TicketId = "T-1",
                RunId = "run-1", State = PrReviewStates.Unread, CheckedAtTicks = 1,
            });
        }
        await ctx.SaveChangesAsync();
    }

    private async Task SeedFilingsAsync(params string[] projects)
    {
        await using var ctx = Context();
        foreach (var project in projects)
            ctx.Add(new ApprovedSeries
            {
                Tracker = "gh", TicketKey = $"{project}#1", TicketId = "1", Project = project, SeriesId = "",
                ContentJson = "{}", ApprovedAt = DateTimeOffset.UnixEpoch, ApprovedInConversation = "c-1",
            });
        await ctx.SaveChangesAsync();
    }

    private async Task SeedRunsAsync(params string[] projects)
    {
        await using var ctx = Context();
        foreach (var project in projects)
            ctx.Runs.Add(new Run { Id = $"run-{project}", Project = project, Pipeline = "code", Status = "success" });
        await ctx.SaveChangesAsync();
    }

    private static SpecDialogSession Session(string id, string project) => new()
    {
        SessionId = id, Platform = "dashboard", UserId = "u", ThreadId = id, Project = project,
    };

    private AgentSmithDbContext Context() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
}
