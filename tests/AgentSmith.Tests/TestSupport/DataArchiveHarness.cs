using System.Globalization;
using System.Text.RegularExpressions;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Services.Archive;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-08-28-2af6: the archive's own graph, resolved the way a host resolves it, so every
/// test runs the registered wiring rather than a hand-assembled copy of it.
/// </summary>
internal sealed class DataArchiveHarness : IDisposable
{
    private readonly ServiceProvider _services;

    internal DataArchiveHarness()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddDataArchive();
        _services = services.BuildServiceProvider(validateScopes: true);
    }

    internal IDataArchiveWriter Writer => _services.GetRequiredService<IDataArchiveWriter>();

    internal IDataArchiveReader Reader => _services.GetRequiredService<IDataArchiveReader>();

    /// <summary>The whole store as a seekable archive, positioned at its start.</summary>
    internal async Task<MemoryStream> ExportAsync(AgentSmithDbContext db)
    {
        var archive = new MemoryStream();
        await Writer.WriteAsync(db, archive);
        archive.Position = 0;
        return archive;
    }

    /// <summary>Every table's rows, canonically encoded and sorted — two stores compare equal
    /// only when every column of every row survived.</summary>
    internal static async Task<Dictionary<string, List<string>>> SnapshotAsync(AgentSmithDbContext db)
    {
        var codec = new ArchiveRowCodec();
        var sets = new EntityTypeSet();
        var snapshot = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var type in Tables(db))
        {
            var rows = new List<string>();
            await foreach (var row in sets.Rows(db, type)) rows.Add(codec.Encode(type, row));
            rows.Sort(StringComparer.Ordinal);
            snapshot[type.GetTableName()!] = rows;
        }

        return snapshot;
    }

    /// <summary>
    /// 2026-10-28-ebb0: <paramref name="snapshot"/> as PostgreSQL can hold it. timestamptz stores
    /// an instant at microsecond resolution — no offset, no 100 ns ticks — so every timestamp is
    /// brought to UTC and cut to the microsecond before two snapshots are compared. Nothing else
    /// is loosened: a value that is not a timestamp still has to match character for character.
    /// </summary>
    internal static Dictionary<string, List<string>> AtPostgresResolution(Dictionary<string, List<string>> snapshot) =>
        snapshot.ToDictionary(
            table => table.Key,
            table => table.Value.Select(row => Timestamp.Replace(row, m => '"' + Microseconds(m.Groups[1].Value) + '"'))
                .Order(StringComparer.Ordinal).ToList(),
            StringComparer.Ordinal);

    private static readonly Regex Timestamp = new(
        @"""(\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(?:\.\d{1,7})?(?:Z|(?:\+|\\u002B|-)\d{2}:\d{2}))""",
        RegexOptions.Compiled);

    private static string Microseconds(string stored)
    {
        var instant = DateTimeOffset.Parse(stored.Replace("\\u002B", "+"), CultureInfo.InvariantCulture).ToUniversalTime();
        return instant.AddTicks(-(instant.Ticks % TimeSpan.TicksPerMicrosecond)).ToString("O", CultureInfo.InvariantCulture);
    }

    internal static IEnumerable<IEntityType> Tables(AgentSmithDbContext db) =>
        db.Model.GetEntityTypes().Where(t => t.GetTableName() is not null);

    public void Dispose() => _services.Dispose();
}
