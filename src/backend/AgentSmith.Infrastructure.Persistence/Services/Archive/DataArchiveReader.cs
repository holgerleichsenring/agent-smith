using System.IO.Compression;
using System.Text.Json;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Persistence.Services.Archive;

/// <summary>
/// 2026-08-28-2af6: writes an archive back into an empty store. It refuses first — the
/// schema head by name, then every table's emptiness — writes the tables in the order the
/// model declares with the audit stamping suspended and the provider's identity insertion
/// switched on where it is needed, advances each generator past the copied keys, and
/// verifies against the manifest before it commits. Anything that fails takes the whole
/// import down with it: one transaction, no half-copied database.
/// </summary>
public sealed class DataArchiveReader(
    ArchiveTableOrder order,
    ArchiveSchemaCheck schema,
    EmptyTargetCheck empty,
    ArchiveTableImporter importer,
    IdentityInsertSwitch identity,
    IdentitySequenceAdvancer advancer,
    ImportedRowCountVerifier verifier,
    ILogger<DataArchiveReader> logger) : IDataArchiveReader
{
    private static readonly JsonSerializerOptions ManifestJson =
        new() { PropertyNameCaseInsensitive = true };

    public async Task<DataArchiveImportReport> ReadAsync(
        AgentSmithDbContext db, Stream archive, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(archive);
        if (!archive.CanSeek)
            throw new DataArchiveException(
                "An archive is read from a seekable source: a zip's directory sits at its end, "
                + "and the manifest has to be read before anything is written.");

        using var zip = new ZipArchive(archive, ZipArchiveMode.Read, leaveOpen: true);
        var manifest = await ManifestAsync(zip, cancellationToken);
        var types = order.Of(db.Model);
        await schema.VerifyAsync(db, manifest, cancellationToken);
        await empty.VerifyAsync(db, types, cancellationToken);
        return await WriteAsync(db, zip, manifest, types, cancellationToken);
    }

    private async Task<DataArchiveImportReport> WriteAsync(
        AgentSmithDbContext db, ZipArchive zip, DataArchiveManifest manifest,
        IReadOnlyList<IEntityType> types, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        using var stamping = db.SuspendAuditStamping();
        var written = new List<ArchivedTable>(types.Count);
        foreach (var type in types) written.Add(await TableAsync(db, zip, type, ct));

        await verifier.VerifyAsync(db, manifest, types, ct);
        await transaction.CommitAsync(ct);
        logger.LogInformation(
            "Imported a data archive at schema {Schema}: {Tables} tables, {Rows} rows.",
            manifest.SchemaHead, written.Count, written.Sum(t => t.Rows));
        return new DataArchiveImportReport(manifest, written);
    }

    private async Task<ArchivedTable> TableAsync(
        AgentSmithDbContext db, ZipArchive zip, IEntityType type, CancellationToken ct)
    {
        var table = type.GetTableName()!;
        var entry = zip.GetEntry(DataArchiveFormat.EntryFor(table))
            ?? throw new DataArchiveException($"The archive holds no file for table '{table}'.");

        await identity.EnableAsync(db, type, ct);
        await using var rows = entry.Open();
        var imported = await importer.ImportAsync(db, type, rows, ct);
        await identity.DisableAsync(db, type, ct);
        await advancer.AdvanceAsync(db, type, imported.MaxKey, ct);
        return new ArchivedTable(table, imported.Rows);
    }

    private static async Task<DataArchiveManifest> ManifestAsync(ZipArchive zip, CancellationToken ct)
    {
        var entry = zip.GetEntry(DataArchiveFormat.ManifestEntry)
            ?? throw new DataArchiveException(
                "The archive carries no manifest, so it is not a data archive.");
        await using var stream = entry.Open();
        var manifest = await JsonSerializer.DeserializeAsync<DataArchiveManifest>(stream, ManifestJson, ct)
            ?? throw new DataArchiveException("The archive's manifest is empty.");
        if (manifest.FormatVersion == DataArchiveFormat.Version) return manifest;

        throw new DataArchiveException(
            $"The archive is in format '{manifest.FormatVersion}' and this build reads format "
            + $"'{DataArchiveFormat.Version}'.");
    }
}
