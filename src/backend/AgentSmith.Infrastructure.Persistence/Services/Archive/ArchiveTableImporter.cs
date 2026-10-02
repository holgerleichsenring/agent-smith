using System.Globalization;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore.Metadata;

namespace AgentSmith.Infrastructure.Persistence.Services.Archive;

/// <summary>
/// 2026-08-28-2af6: writes one table's lines into the store, in batches, and reports how
/// many arrived and the largest key among them. It writes through the application's own
/// persistence layer, so every value is bound as a parameter of its declared type and no
/// dialect's literal syntax is involved anywhere.
/// <para>
/// The caller suspends the context's audit stamping around the whole import: through the
/// plain save path every row would be given the wall-clock of the import while the counts
/// still matched.
/// </para>
/// </summary>
public sealed class ArchiveTableImporter(ArchiveRowCodec codec, GeneratedKeyProperty generatedKey)
{
    // Big enough that a large table is not a round trip per row, small enough that the
    // change tracker never holds a table.
    private const int BatchSize = 500;

    // 2026-10-01-283da: and a batch is flushed sooner when its content reaches this, so five
    // hundred five-megabyte reference files are never tracked at once.
    internal const long BatchContentBytes = 64L * 1024 * 1024;

    public async Task<ImportedTableRows> ImportAsync(
        AgentSmithDbContext db, IEntityType type, Stream rows, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(db);
        var key = generatedKey.Of(type);
        using var lines = new StreamReader(rows);
        var content = ContentProperties(type);
        var written = 0L;
        var maxKey = 0L;
        var pending = (Rows: 0, Bytes: 0L);
        while (await lines.ReadLineAsync(ct) is { } line)
        {
            if (line.Length == 0) continue;
            var row = codec.Decode(type, line);
            db.Add(row);
            maxKey = Math.Max(maxKey, KeyOf(key, row));
            written++;
            pending = (pending.Rows + 1, pending.Bytes + ContentOf(content, row));
            if (pending.Rows < BatchSize && pending.Bytes < BatchContentBytes) continue;
            await FlushAsync(db, ct);
            pending = (0, 0L);
        }

        await FlushAsync(db, ct);
        return new ImportedTableRows(written, maxKey);
    }

    private static async Task FlushAsync(AgentSmithDbContext db, CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();
    }

    private static System.Reflection.PropertyInfo[] ContentProperties(IEntityType type) =>
        [.. type.GetProperties()
            .Where(p => p.ClrType == typeof(byte[]) || p.ClrType == typeof(string))
            .Select(p => p.PropertyInfo).OfType<System.Reflection.PropertyInfo>()];

    // The decoded size of a row's content: its bytes, and its text by character.
    private static long ContentOf(System.Reflection.PropertyInfo[] content, object row) =>
        content.Sum(p => p.GetValue(row) switch
        {
            byte[] bytes => bytes.LongLength,
            string text => text.Length,
            _ => 0L,
        });

    private static long KeyOf(IProperty? key, object row) =>
        key?.PropertyInfo?.GetValue(row) is { } value
            ? Convert.ToInt64(value, CultureInfo.InvariantCulture)
            : 0;
}
