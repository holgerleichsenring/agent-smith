using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
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
