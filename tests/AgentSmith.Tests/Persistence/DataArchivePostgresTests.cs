using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-10-28-ebb0: the PostgreSQL leg of the archive journey — a populated SQLite store
/// written into a freshly migrated PostgreSQL database, every row of every table intact, and
/// a database whose identity sequences are past the copied keys. Intact means every value
/// unchanged except timestamps, which arrive as the same instant at PostgreSQL's resolution:
/// UTC, to the microsecond.
/// <para>
/// It needs a server, so it is opt-in through <see cref="RequiresPostgresFactAttribute"/> and
/// reports itself as skipped-with-a-reason when there is none. It creates its OWN database
/// and drops it again.
/// </para>
/// </summary>
public sealed class DataArchivePostgresTests : IDisposable
{
    private readonly SqliteConnection _source = MigratedStoreTemplate.OpenCopy();
    private readonly DataArchiveHarness _archive = new();

    [RequiresPostgresFact]
    public async Task RoundTrip_SqliteToPostgres_RestoresEveryRow()
    {
        Dictionary<string, List<string>> expected;
        MemoryStream archive;
        await using (var source = MigratedStoreTemplate.Context(_source))
        {
            await new FullStoreSeed().SeedAsync(source);
            expected = await DataArchiveHarness.SnapshotAsync(source);
            archive = await _archive.ExportAsync(source);
        }

        await using var target = await ScratchPostgres.MigratedAsync(
            RequiresPostgresFactAttribute.ConnectionString!, "archive");
        try
        {
            using (archive) await _archive.Reader.ReadAsync(target, archive);

            DataArchiveHarness.AtPostgresResolution(await DataArchiveHarness.SnapshotAsync(target))
                .Should().BeEquivalentTo(DataArchiveHarness.AtPostgresResolution(expected),
                    "every column of every row must cross the providers, timestamps as the same instant");
            (await target.RunArtifacts.SingleAsync(a => a.Id == FullStoreSeed.ShellSubstitutionArtifactId))
                .CreatedAt.Should().BeCloseTo(FullStoreSeed.SeededAt, TimeSpan.FromMicroseconds(1),
                    "the audit timestamp is the archive's, not the import's");
            await TheNextRowStillGetsAKeyAsync(target);
        }
        finally
        {
            await target.Database.EnsureDeletedAsync();
        }
    }

    [RequiresPostgresFact]
    public async Task Migrate_FreshDatabase_SeedsTheReferenceFileIdentityAboveTheLegacyRange()
    {
        await using var db = await ScratchPostgres.MigratedAsync(
            RequiresPostgresFactAttribute.ConnectionString!, "seed");
        try
        {
            db.Set<ReferenceFile>().Add(new ReferenceFile
            {
                SessionId = "s", Kind = "upload", RelativePath = "a.txt", MediaType = "text/plain", Content = [1],
            });
            await db.SaveChangesAsync();

            (await db.Set<ReferenceFile>().SingleAsync()).Id.Should().Be(ReferenceFileIdentity.Seed,
                "every id below the seed belongs to the legacy image table");
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }

    private static async Task TheNextRowStillGetsAKeyAsync(AgentSmithDbContext target)
    {
        target.RunArtifacts.Add(new RunArtifact { RunId = "run-after", Kind = "after", Content = "x" });
        var act = async () => await target.SaveChangesAsync();

        await act.Should().NotThrowAsync("the identity sequence must be past the copied keys");
        (await target.RunArtifacts.SingleAsync(a => a.RunId == "run-after"))
            .Id.Should().BeGreaterThan(FullStoreSeed.VeryLongArtifactId);
    }

    public void Dispose()
    {
        _archive.Dispose();
        _source.Dispose();
    }
}
