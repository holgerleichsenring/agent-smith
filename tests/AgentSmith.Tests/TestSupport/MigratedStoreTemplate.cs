using AgentSmith.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// p0432: the suite applies its migration set ONCE per run and hands each test class a copy.
/// <para>
/// Applying every migration is the most expensive thing any test here does, and it was
/// O(classes): forty-one sites called <c>Database.Migrate()</c>, so every test that needed a
/// store added another full migration run to every CI run, forever. It had already cost two
/// CI cycles, and neither victim was a database test — on a two-core runner the migration
/// burst starves async continuations, and the first casualty is whichever test asserts a
/// wall-clock bound (#501 lost a 50 ms send bound; #502 lost a monotonic-gap window).
/// </para>
/// <para>
/// SQLite copies a database far faster than it builds one. The template is migrated once,
/// behind a lazy, and each caller gets a fresh in-memory database restored from it via
/// <c>BackupDatabase</c> — the engine's own page copy. Each test still gets its OWN
/// database: a copy is not a share.
/// </para>
/// </summary>
internal static class MigratedStoreTemplate
{
    // The template is never written to, but SQLite connections are not thread-safe and
    // xUnit runs classes in parallel, so the copy itself is serialized. Copying is the
    // cheap half; migrating was the expensive one, and that now happens once.
    private static readonly Lock Gate = new();

    private static readonly Lazy<SqliteConnection> Template = new(
        Migrated, LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>A fresh, migrated, private in-memory store. The caller owns and disposes it.</summary>
    internal static SqliteConnection OpenCopy()
    {
        var fresh = new SqliteConnection("Data Source=:memory:");
        fresh.Open();
        lock (Gate) Template.Value.BackupDatabase(fresh);
        return fresh;
    }

    /// <summary>
    /// The same migrated schema, restored into a database ON DISK — for a case that hands a
    /// connection STRING to something that opens its own connection, such as a booted server.
    /// </summary>
    internal static void CopyToFile(string path)
    {
        using var file = new SqliteConnection($"Data Source={path}");
        file.Open();
        lock (Gate) Template.Value.BackupDatabase(file);
    }

    /// <summary>How many times the migration set was applied in this process. One, or the
    /// template stopped being shared — which is the whole cost this class exists to remove.</summary>
    internal static int TimesMigrated => _timesMigrated;

    /// <summary>A context over a store this class opened — the same shape the tests used inline.</summary>
    internal static AgentSmithDbContext Context(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(connection).Options);

    private static SqliteConnection Migrated()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var ctx = Context(connection);
        ctx.Database.Migrate();
        Interlocked.Increment(ref _timesMigrated);
        return connection;
    }

    private static int _timesMigrated;
}
