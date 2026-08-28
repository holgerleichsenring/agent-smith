using System.IO.Compression;
using System.Text;
using System.Text.Json;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Persistence.Services.Archive;

/// <summary>
/// 2026-08-28-2af6: writes the whole store into one zip — the manifest first, then one
/// line-delimited file per table, a row at a time.
/// <para>
/// The counts and the rows are read inside ONE transaction, because a running
/// installation writes trail rows continuously and a count taken in a pre-pass would
/// disagree with rows streamed afterwards. Where the provider's isolation cannot hold
/// that still, the writer catches it itself: a table whose streamed row count differs
/// from the count in the manifest fails the EXPORT, rather than leaving a good-looking
/// archive to fail an import hours later.
/// </para>
/// </summary>
public sealed class DataArchiveWriter(
    ArchiveTableOrder order,
    EntityTypeSet tables,
    ArchiveRowCodec codec,
    MigrationHeadName head,
    TimeProvider clock,
    ILogger<DataArchiveWriter> logger) : IDataArchiveWriter
{
    private static readonly string AppVersion =
        typeof(DataArchiveWriter).Assembly.GetName().Version?.ToString() ?? "0.0.0";

    private static readonly JsonSerializerOptions ManifestJson = new() { WriteIndented = true };

    public async Task<DataArchiveManifest> WriteAsync(
        AgentSmithDbContext db, Stream destination, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        var types = order.Of(db.Model);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var manifest = await ManifestAsync(db, types, cancellationToken);
        using (var zip = new ZipArchive(destination, ZipArchiveMode.Create, leaveOpen: true))
        {
            await WriteManifestAsync(zip, manifest, cancellationToken);
            foreach (var type in types)
                await WriteTableAsync(zip, db, type, Expected(manifest, type), cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation(
            "Wrote a data archive at schema {Schema}: {Tables} tables, {Rows} rows.",
            manifest.SchemaHead, manifest.Tables.Count, manifest.Tables.Sum(t => t.Rows));
        return manifest;
    }

    private async Task<DataArchiveManifest> ManifestAsync(
        AgentSmithDbContext db, IReadOnlyList<IEntityType> types, CancellationToken ct)
    {
        var counts = new List<ArchivedTable>(types.Count);
        foreach (var type in types)
            counts.Add(new ArchivedTable(TableOf(type), await tables.CountAsync(db, type, ct)));

        return new DataArchiveManifest
        {
            FormatVersion = DataArchiveFormat.Version,
            SchemaHead = head.Of(await db.Database.GetAppliedMigrationsAsync(ct)),
            SourceProvider = db.Database.ProviderName ?? string.Empty,
            AppVersion = AppVersion,
            TakenAt = clock.GetUtcNow(),
            Tables = counts,
        };
    }

    private static async Task WriteManifestAsync(
        ZipArchive zip, DataArchiveManifest manifest, CancellationToken ct)
    {
        var entry = zip.CreateEntry(DataArchiveFormat.ManifestEntry, CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await JsonSerializer.SerializeAsync(stream, manifest, ManifestJson, ct);
    }

    private async Task WriteTableAsync(
        ZipArchive zip, AgentSmithDbContext db, IEntityType type, long expected, CancellationToken ct)
    {
        var entry = zip.CreateEntry(DataArchiveFormat.EntryFor(TableOf(type)), CompressionLevel.Optimal);
        await using var stream = entry.Open();
        await using var lines = new StreamWriter(stream, new UTF8Encoding(false));
        var written = 0L;
        await foreach (var row in tables.Rows(db, type).WithCancellation(ct))
        {
            await lines.WriteLineAsync(codec.Encode(type, row).AsMemory(), ct);
            written++;
        }

        await lines.FlushAsync(ct);
        if (written != expected)
            throw new DataArchiveException(
                $"Table '{TableOf(type)}' held {expected} rows when the manifest was written and "
                + $"streamed {written}. Stop everything writing to this database and export again.");
    }

    private static long Expected(DataArchiveManifest manifest, IEntityType type) =>
        manifest.Tables.Single(t => t.Table == TableOf(type)).Rows;

    private static string TableOf(IEntityType type) => type.GetTableName()!;
}
