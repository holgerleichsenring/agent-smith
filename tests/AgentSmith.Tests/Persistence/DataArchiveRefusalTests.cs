using AgentSmith.Domain.Exceptions;
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
/// 2026-08-28-2af6: what the import refuses, and that it refuses BEFORE it writes. A
/// half-copied database is the failure this phase exists to prevent, so every refusal is
/// asserted together with the target still being empty afterwards.
/// </summary>
public sealed class DataArchiveRefusalTests : IDisposable
{
    private readonly SqliteConnection _source = MigratedStoreTemplate.OpenCopy();
    private readonly SqliteConnection _target = MigratedStoreTemplate.OpenCopy();
    private readonly DataArchiveHarness _archive = new();

    [Fact]
    public async Task Import_ASchemaStateThatDiffers_RefusesBeforeWriting()
    {
        using var archive = await ExportSeededAsync();
        using var tampered = RewrittenArchive.WithManifest(
            archive, m => m with { SchemaHead = "ASchemaThisBuildHasNeverSeen" });

        var act = () => ImportAsync(tampered);

        (await act.Should().ThrowAsync<DataArchiveException>()).Which.Message
            .Should().Contain("ASchemaThisBuildHasNeverSeen").And.Contain("AddObservedCallers");
        await TargetShouldBeEmptyAsync();
    }

    [Fact]
    public async Task Import_ATargetHoldingRows_RefusesAndNamesTheTable()
    {
        using var archive = await ExportSeededAsync();
        await using (var target = MigratedStoreTemplate.Context(_target))
        {
            target.Runs.Add(new Run { Id = "already-here", Project = "p", Pipeline = "x", TicketId = "T" });
            await target.SaveChangesAsync();
        }

        var act = () => ImportAsync(archive);

        (await act.Should().ThrowAsync<DataArchiveException>()).Which.Message
            .Should().Contain("Runs").And.Contain("merging is not supported");
        await using var db = MigratedStoreTemplate.Context(_target);
        (await db.Runs.CountAsync()).Should().Be(1, "nothing may be added to a refused target");
    }

    [Fact]
    public async Task Import_ARowCountThatDisagrees_FailsAndNamesTheTable()
    {
        using var archive = await ExportSeededAsync();
        using var tampered = RewrittenArchive.WithManifest(archive, Overstated);

        var act = () => ImportAsync(tampered);

        (await act.Should().ThrowAsync<DataArchiveException>()).Which.Message
            .Should().Contain("RunArtifacts").And.Contain("manifest 99");
        await TargetShouldBeEmptyAsync();
    }

    [Fact]
    public async Task Import_AnArchiveWithoutAManifest_Refuses()
    {
        using var archive = RewrittenArchive.WithoutManifest();

        var act = () => ImportAsync(archive);

        (await act.Should().ThrowAsync<DataArchiveException>()).Which.Message
            .Should().Contain("no manifest");
    }

    [Fact]
    public async Task Import_AStreamThatCannotBeSeeked_Refuses()
    {
        await using var db = MigratedStoreTemplate.Context(_target);
        await using var forwardOnly = new ForwardOnlyStream();

        var act = () => _archive.Reader.ReadAsync(db, forwardOnly);

        (await act.Should().ThrowAsync<DataArchiveException>()).Which.Message
            .Should().Contain("seekable");
    }

    [Fact]
    public async Task Export_TheManifestCounts_MatchWhatWasStreamed()
    {
        await using var db = MigratedStoreTemplate.Context(_source);
        await new FullStoreSeed().SeedAsync(db);
        using var archive = new MemoryStream();

        var manifest = await _archive.Writer.WriteAsync(db, archive);

        manifest.Tables.Sum(t => t.Rows).Should().Be(46);
    }

    private static DataArchiveManifest Overstated(DataArchiveManifest manifest) =>
        manifest with
        {
            Tables = [.. manifest.Tables.Select(t => t.Table == "RunArtifacts"
                ? t with { Rows = 99 }
                : t)],
        };

    private async Task TargetShouldBeEmptyAsync()
    {
        await using var db = MigratedStoreTemplate.Context(_target);
        foreach (var type in DataArchiveHarness.Tables(db))
            (await new EntityTypeSet().CountAsync(db, type, CancellationToken.None))
                .Should().Be(0, "a refused import writes nothing at all");
    }

    private async Task<MemoryStream> ExportSeededAsync()
    {
        await using var db = MigratedStoreTemplate.Context(_source);
        await new FullStoreSeed().SeedAsync(db);
        return await _archive.ExportAsync(db);
    }

    private async Task ImportAsync(Stream archive)
    {
        await using var db = MigratedStoreTemplate.Context(_target);
        await _archive.Reader.ReadAsync(db, archive);
    }

    public void Dispose()
    {
        _archive.Dispose();
        _source.Dispose();
        _target.Dispose();
    }

    private sealed class ForwardOnlyStream : MemoryStream
    {
        public override bool CanSeek => false;
    }
}
