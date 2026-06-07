using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// p0246a: the persistence foundation proven on a REAL SQLite engine (NOT
/// EF-InMemory — that does not enforce unique indexes, so it cannot prove the
/// single-run invariant). Each test runs the InitialCreate migration against a
/// private in-memory database, so the schema under test is exactly what ships.
/// </summary>
public sealed class PersistenceFoundationTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public PersistenceFoundationTests()
    {
        // A single open connection keeps the in-memory database alive for the
        // test; multiple DbContexts over it model separate units of work.
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using var ctx = NewContext();
        ctx.Database.Migrate();
    }

    private AgentSmithDbContext NewContext()
    {
        var options = new DbContextOptionsBuilder<AgentSmithDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new AgentSmithDbContext(options);
    }

    [Fact]
    public void Migration_AppliesOnSqlite_IncludingActiveRunKeyLength()
    {
        using var ctx = NewContext();

        // The migration applied in the ctor; the unique-indexed ActiveRuns table
        // is queryable, which means the schema (incl. the key-length-capped
        // unique index) was created without error.
        var act = () => ctx.ActiveRuns.Any();

        act.Should().NotThrow("the InitialCreate migration must apply cleanly on SQLite");
        ctx.ActiveRuns.Should().BeEmpty();
    }

    [Fact]
    public async Task Claim_TwoConcurrentForSameTicket_OnlyOneActiveRow_OnSqlite()
    {
        await SeedRunAsync("run-A");
        await SeedRunAsync("run-B");

        await ClaimAsync("proj", "T-1", "run-A");
        var second = await TryClaimAsync("proj", "T-1", "run-B");

        second.Should().BeFalse("the UNIQUE(Project,TicketId) index must refuse the second claim");
        using var verify = NewContext();
        verify.ActiveRuns.Count(a => a.Project == "proj" && a.TicketId == "T-1")
            .Should().Be(1, "exactly one run may hold the ticket lease");
    }

    [Fact]
    public async Task UniqueViolation_Sqlite_MapsToAlreadyClaimed()
    {
        await SeedRunAsync("run-A");
        await SeedRunAsync("run-B");
        await ClaimAsync("proj", "T-1", "run-A");

        using var ctx = NewContext();
        ctx.ActiveRuns.Add(new ActiveRun { Project = "proj", TicketId = "T-1", RunId = "run-B" });
        var translator = new SqliteUniqueViolationTranslator();

        var caught = await Record.ExceptionAsync(() => ctx.SaveChangesAsync());

        caught.Should().BeOfType<DbUpdateException>();
        translator.IsUniqueViolation(caught!)
            .Should().BeTrue("a SQLite unique violation (code 19/2067) maps to AlreadyClaimed");
    }

    [Fact]
    public async Task DbContext_RoundTripsRunWithChildren_OnSqlite()
    {
        using (var ctx = NewContext())
        {
            var run = new Run
            {
                Id = "run-1", Project = "proj", Pipeline = "fix-bug", TicketId = "T-1",
                Status = "running", StartedAt = DateTimeOffset.UtcNow,
            };
            // Children are keyed by RunId (no FK relationship), so they are added
            // via their own DbSets, not through the unmapped Run collections.
            ctx.Runs.Add(run);
            ctx.RunSteps.Add(new RunStep { RunId = "run-1", StepIndex = 0, StepName = "LoadCatalog", Status = "ok" });
            ctx.RunRepos.Add(new RunRepo { RunId = "run-1", RepoName = "primary", ChangeCount = 2 });
            ctx.RunArtifacts.Add(new RunArtifact { RunId = "run-1", Kind = "result_md", Content = "# Result" });
            await ctx.SaveChangesAsync();
        }

        using var read = NewContext();
        var loaded = read.Runs.Single(r => r.Id == "run-1");

        read.RunSteps.Where(s => s.RunId == "run-1").Should().ContainSingle(s => s.StepName == "LoadCatalog");
        read.RunRepos.Where(r => r.RunId == "run-1").Should().ContainSingle(r => r.RepoName == "primary" && r.ChangeCount == 2);
        read.RunArtifacts.Where(a => a.RunId == "run-1").Should().ContainSingle(a => a.Kind == "result_md" && a.Content == "# Result");
        loaded.CreatedAt.Should().NotBe(default, "SaveChanges must stamp the audit columns");
    }

    private async Task SeedRunAsync(string runId)
    {
        using var ctx = NewContext();
        ctx.Runs.Add(new Run
        {
            Id = runId, Project = "proj", Pipeline = "fix-bug", TicketId = "T-1",
            Status = "running", StartedAt = DateTimeOffset.UtcNow,
        });
        await ctx.SaveChangesAsync();
    }

    private async Task ClaimAsync(string project, string ticket, string runId)
    {
        using var ctx = NewContext();
        ctx.ActiveRuns.Add(new ActiveRun { Project = project, TicketId = ticket, RunId = runId });
        await ctx.SaveChangesAsync();
    }

    private async Task<bool> TryClaimAsync(string project, string ticket, string runId)
    {
        var translator = new SqliteUniqueViolationTranslator();
        try
        {
            await ClaimAsync(project, ticket, runId);
            return true;
        }
        catch (DbUpdateException ex) when (translator.IsUniqueViolation(ex))
        {
            return false;
        }
    }

    public void Dispose() => _connection.Dispose();
}
