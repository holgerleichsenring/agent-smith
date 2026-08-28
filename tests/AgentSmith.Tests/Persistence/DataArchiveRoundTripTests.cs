using System.IO.Compression;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Infrastructure.Persistence.Services.Archive;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-08-28-2af6: the central claim — a real database round-trips. Every table of a
/// populated store is written into one archive and read back into an empty one, and the
/// two stores are then compared column by column rather than by row count, because an
/// import through the plain save path matches on counts while destroying every timestamp.
/// <para>
/// The leg proven here is SQLite to SQLite, unconditionally. The cross-provider leg lives
/// in <see cref="DataArchiveSqlServerTests"/>, which needs a database to be handed to it.
/// </para>
/// </summary>
public sealed class DataArchiveRoundTripTests : IDisposable
{
    private readonly SqliteConnection _source = MigratedStoreTemplate.OpenCopy();
    private readonly SqliteConnection _target = MigratedStoreTemplate.OpenCopy();
    private readonly DataArchiveHarness _archive = new();

    [Fact]
    public async Task RoundTrip_EveryTable_RestoresEveryRow()
    {
        var expected = await SeededSnapshotAsync();
        expected.Should().HaveCount(22);
        expected.Values.Sum(rows => rows.Count).Should().Be(46,
            "two rows per table plus the two shapes that broke the hand transfer");

        await ImportAsync(await ExportSeededAsync());

        await using var target = MigratedStoreTemplate.Context(_target);
        var actual = await DataArchiveHarness.SnapshotAsync(target);
        actual.Should().BeEquivalentTo(expected,
            "every column of every row must arrive exactly as the archive carried it");
    }

    [Fact]
    public async Task Archive_EveryTableInTheStore_IsWrittenToTheArchive()
    {
        using var archive = await ExportSeededAsync();

        using var zip = new ZipArchive(archive, ZipArchiveMode.Read, leaveOpen: true);
        await using var db = MigratedStoreTemplate.Context(_source);
        var expected = DataArchiveHarness.Tables(db)
            .Select(t => DataArchiveFormat.EntryFor(t.GetTableName()!)).ToList();
        expected.Should().HaveCount(22, "the store carries twenty-two tables");
        zip.Entries.Select(e => e.FullName).Should().Contain(expected);
    }

    [Fact]
    public async Task Archive_TheManifest_IsTheFirstEntryInTheZip()
    {
        using var archive = await ExportSeededAsync();

        using var zip = new ZipArchive(archive, ZipArchiveMode.Read, leaveOpen: true);
        zip.Entries[0].FullName.Should().Be(DataArchiveFormat.ManifestEntry,
            "a reader must meet the manifest before any table");
    }

    [Fact]
    public async Task Archive_TheManifest_NamesTheSchemaStateAndRowCounts()
    {
        await SeedAsync();
        await using var db = MigratedStoreTemplate.Context(_source);
        using var archive = new MemoryStream();

        var manifest = await _archive.Writer.WriteAsync(db, archive);

        manifest.SchemaHead.Should().NotBeEmpty().And.NotContain("_",
            "the head is named, not identified — the timestamp prefix is provider-local");
        manifest.SourceProvider.Should().Be("Microsoft.EntityFrameworkCore.Sqlite");
        manifest.FormatVersion.Should().Be(DataArchiveFormat.Version);
        manifest.Tables.Should().HaveCount(22);
        manifest.Tables.Single(t => t.Table == "RunArtifacts").Rows.Should().Be(4,
            "two generated rows plus the two shapes that broke the hand transfer");
    }

    [Fact]
    public async Task RoundTrip_TheAuditTimestamps_AreTheOnesTheArchiveCarried()
    {
        await ImportAsync(await ExportSeededAsync());

        await using var db = MigratedStoreTemplate.Context(_target);
        var artifact = await db.RunArtifacts.SingleAsync(a => a.Id == FullStoreSeed.ShellSubstitutionArtifactId);
        artifact.CreatedAt.Should().Be(FullStoreSeed.SeededAt,
            "the import writes with the audit stamping suspended, or every row silently "
            + "takes the wall-clock of the import while the counts still match");
        artifact.UpdatedAt.Should().Be(FullStoreSeed.SeededAt.AddMinutes(1));
    }

    [Fact]
    public async Task RoundTrip_ThePrimaryKeys_AreTheOnesTheArchiveCarried()
    {
        await ImportAsync(await ExportSeededAsync());

        await using var db = MigratedStoreTemplate.Context(_target);
        var keys = await db.RunArtifacts.Select(a => a.Id).OrderBy(id => id).ToListAsync();
        keys.Should().Contain(FullStoreSeed.ShellSubstitutionArtifactId)
            .And.Contain(FullStoreSeed.VeryLongArtifactId)
            .And.NotContain(1, "a regenerated key would start at one");
    }

    [Fact]
    public async Task RoundTrip_ARowWithShellSubstitutionText_SurvivesUnchanged()
    {
        await ImportAsync(await ExportSeededAsync());

        await using var db = MigratedStoreTemplate.Context(_target);
        var artifact = await db.RunArtifacts.SingleAsync(a => a.Id == FullStoreSeed.ShellSubstitutionArtifactId);
        artifact.Content.Should().Be(FullStoreSeed.ShellSubstitutionText,
            "a dollar-parenthesis sequence is data here, not a client tool's substitution");
    }

    [Fact]
    public async Task RoundTrip_AVeryLongArtifactRow_SurvivesUnchanged()
    {
        await ImportAsync(await ExportSeededAsync());

        await using var db = MigratedStoreTemplate.Context(_target);
        var artifact = await db.RunArtifacts.SingleAsync(a => a.Id == FullStoreSeed.VeryLongArtifactId);
        artifact.Content.Should().HaveLength(FullStoreSeed.VeryLongBody.Length)
            .And.Be(FullStoreSeed.VeryLongBody);
        FullStoreSeed.VeryLongBody.Length.Should().BeGreaterThan(50_000,
            "the row that broke the hand transfer was 50,728 characters");
    }

    [Fact]
    public async Task Import_TheNextInsertAfterAnImport_GetsAFreeKey()
    {
        await ImportAsync(await ExportSeededAsync());

        await using var db = MigratedStoreTemplate.Context(_target);
        db.RunArtifacts.Add(new RunArtifact { RunId = "run-after", Kind = "after", Content = "x" });
        var act = async () => await db.SaveChangesAsync();

        await act.Should().NotThrowAsync("the generator must be past the copied rows");
        var written = await db.RunArtifacts.SingleAsync(a => a.RunId == "run-after");
        written.Id.Should().BeGreaterThan(FullStoreSeed.VeryLongArtifactId);
    }

    [Fact]
    public async Task Import_WritesInDependencyOrder_SoForeignKeysHold()
    {
        await using var source = MigratedStoreTemplate.Context(_source);
        var order = new ArchiveTableOrder().Of(source.Model).Select(t => t.GetTableName()).ToList();

        order.IndexOf("ConfigEntities").Should().BeLessThan(order.IndexOf("ConfigRefs"),
            "the one enforced foreign key in the model points config_ref at config_entity");

        await ImportAsync(await ExportSeededAsync());
        await using var target = MigratedStoreTemplate.Context(_target);
        (await target.ConfigRefs.CountAsync()).Should().Be(2);
    }

    private async Task<Dictionary<string, List<string>>> SeededSnapshotAsync()
    {
        await SeedAsync();
        await using var db = MigratedStoreTemplate.Context(_source);
        return await DataArchiveHarness.SnapshotAsync(db);
    }

    // Seeding is idempotent per test: a table's unique indexes would refuse a second copy,
    // and several tests both snapshot the source and export it.
    private async Task SeedAsync()
    {
        if (_seeded) return;
        _seeded = true;
        await using var db = MigratedStoreTemplate.Context(_source);
        await new FullStoreSeed().SeedAsync(db);
    }

    private bool _seeded;

    private async Task<MemoryStream> ExportSeededAsync()
    {
        await SeedAsync();
        await using var db = MigratedStoreTemplate.Context(_source);
        return await _archive.ExportAsync(db);
    }

    private async Task<DataArchiveImportReport> ImportAsync(MemoryStream archive)
    {
        using (archive)
        {
            await using var db = MigratedStoreTemplate.Context(_target);
            return await _archive.Reader.ReadAsync(db, archive);
        }
    }

    public void Dispose()
    {
        _archive.Dispose();
        _source.Dispose();
        _target.Dispose();
    }
}
