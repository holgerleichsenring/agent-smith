using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Services.Archive;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-09-17-042ej: the filed-work read looks a WORK TICKET up across the projects of one
/// tracker and runs on every nudge window of a live run, so the runs table is indexed on
/// (Project, TicketId) rather than on Project alone.
/// <para>
/// The two providers keep separate migration assemblies with different timestamp prefixes on
/// the same migration, and an archive crossing them compares the head by NAME — so one index
/// is one migration of ONE NAME in both.
/// </para>
/// </summary>
public sealed class RunTicketIndexTests
{
    private const string Migration = "AddRunTicketIndex";

    [Fact]
    public void Migrations_BothProviders_HeadNamesStillCompareEqual()
    {
        var head = new MigrationHeadName();

        var sqlite = head.Of(Migrations(PersistenceProvider.Sqlite));
        var sqlServer = head.Of(Migrations(PersistenceProvider.SqlServer));

        sqlite.Should().Be(sqlServer, "an archive must be able to cross the providers");
        sqlite.Should().NotBeEmpty();
    }

    /// <summary>
    /// The claim this phase adds, and it is about CONTAINMENT rather than the head: the next
    /// migration anywhere moves the head, and a test that pinned it would go red for a change
    /// that has nothing to do with this index.
    /// </summary>
    [Fact]
    public void Migrations_BothProviders_CarryThisIndexUnderOneName()
    {
        Named(Migrations(PersistenceProvider.Sqlite)).Should().ContainSingle()
            .Which.Should().Be(Migration);
        Named(Migrations(PersistenceProvider.SqlServer)).Should().ContainSingle()
            .Which.Should().Be(Migration);
    }

    // A migration id is "<timestamp>_<name>"; the archive compares the NAME across providers.
    private static IReadOnlyList<string> Named(IReadOnlyList<string> ids) =>
        [.. ids.Select(id => id.Split('_', 2).Last()).Where(name => name == Migration)];

    [Fact]
    public void RunTicketIndex_OnTheModel_CoversProjectAndTicketIdTogether()
    {
        using var db = new AgentSmithDbContext(Options(PersistenceProvider.Sqlite));

        var indexes = db.Model.FindEntityType(typeof(Run))!.GetIndexes()
            .Select(index => index.Properties.Select(p => p.Name).ToList())
            .ToList();

        indexes.Should().ContainEquivalentOf(new List<string> { "Project", "TicketId" });
    }

    private static IReadOnlyList<string> Migrations(PersistenceProvider provider)
    {
        using var db = new AgentSmithDbContext(Options(provider));
        return [.. db.Database.GetMigrations().OrderBy(id => id, StringComparer.Ordinal)];
    }

    private static DbContextOptions<AgentSmithDbContext> Options(PersistenceProvider provider)
    {
        var builder = new DbContextOptionsBuilder<AgentSmithDbContext>();
        builder.UseProvider(new PersistenceOptions
        {
            Provider = provider,
            ConnectionString = provider == PersistenceProvider.Sqlite
                ? "Data Source=:memory:"
                : "Server=localhost;Database=x;TrustServerCertificate=True",
        });
        return builder.Options;
    }
}
