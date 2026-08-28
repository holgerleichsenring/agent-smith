using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-08-28-2af6: the journey the archive exists for — a populated SQLite store written
/// into a freshly migrated SQL Server database, every row intact, with the keys and the
/// audit timestamps the archive carried and a database that can still take its next row.
/// <para>
/// It needs a server, so it is opt-in through <see cref="RequiresSqlServerFactAttribute"/>
/// and reports itself as skipped-with-a-reason when there is none. It creates its OWN
/// database (name derived per run) and drops it again, so it never touches an existing one.
/// </para>
/// </summary>
public sealed class DataArchiveSqlServerTests : IDisposable
{
    private readonly SqliteConnection _source = MigratedStoreTemplate.OpenCopy();
    private readonly DataArchiveHarness _archive = new();

    [RequiresSqlServerFact]
    public async Task RoundTrip_SqliteToSqlServer_RestoresEveryRow()
    {
        Dictionary<string, List<string>> expected;
        MemoryStream archive;
        await using (var source = MigratedStoreTemplate.Context(_source))
        {
            await new FullStoreSeed().SeedAsync(source);
            expected = await DataArchiveHarness.SnapshotAsync(source);
            archive = await _archive.ExportAsync(source);
        }

        await using var target = await MigratedSqlServerAsync();
        try
        {
            using (archive) await _archive.Reader.ReadAsync(target, archive);

            (await DataArchiveHarness.SnapshotAsync(target)).Should().BeEquivalentTo(expected,
                "every column of every row must cross the providers unchanged");
            await KeysAndTimestampsSurvivedAsync(target);
            await TheNextRowStillGetsAKeyAsync(target);
        }
        finally
        {
            await target.Database.EnsureDeletedAsync();
        }
    }

    private static async Task KeysAndTimestampsSurvivedAsync(AgentSmithDbContext target)
    {
        var artifact = await target.RunArtifacts
            .SingleAsync(a => a.Id == FullStoreSeed.ShellSubstitutionArtifactId);
        artifact.CreatedAt.Should().Be(FullStoreSeed.SeededAt,
            "SQL Server would otherwise have been given the wall-clock of the import");
        artifact.Content.Should().Be(FullStoreSeed.ShellSubstitutionText);
        (await target.RunArtifacts.SingleAsync(a => a.Id == FullStoreSeed.VeryLongArtifactId))
            .Content.Should().Be(FullStoreSeed.VeryLongBody);
    }

    private static async Task TheNextRowStillGetsAKeyAsync(AgentSmithDbContext target)
    {
        target.RunArtifacts.Add(new RunArtifact { RunId = "run-after", Kind = "after", Content = "x" });
        var act = async () => await target.SaveChangesAsync();

        await act.Should().NotThrowAsync("the identity generator must be past the copied keys");
        (await target.RunArtifacts.SingleAsync(a => a.RunId == "run-after"))
            .Id.Should().BeGreaterThan(FullStoreSeed.VeryLongArtifactId);
    }

    // Its own database, named per run, migrated by the shipped SQL Server migration set.
    private static async Task<AgentSmithDbContext> MigratedSqlServerAsync()
    {
        var connection = new SqlConnectionStringBuilder(RequiresSqlServerFactAttribute.ConnectionString)
        {
            InitialCatalog = $"agentsmith_archive_{Guid.NewGuid():N}"[..32],
        };
        var builder = new DbContextOptionsBuilder<AgentSmithDbContext>();
        builder.UseProvider(new PersistenceOptions
        {
            Provider = PersistenceProvider.SqlServer,
            ConnectionString = connection.ConnectionString,
        });
        builder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        var db = new AgentSmithDbContext(builder.Options);
        await db.Database.MigrateAsync();
        return db;
    }

    public void Dispose()
    {
        _archive.Dispose();
        _source.Dispose();
    }
}
