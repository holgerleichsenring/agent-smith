using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.Archive;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Persistence.ReferenceFiles;

/// <summary>
/// 2026-10-01-283da: a migrated store the reference-file tests can open several contexts on —
/// a SQLite FILE always (two copiers need two connections), and a scratch SQL Server database
/// whenever AGENTSMITH_TEST_DB_CONNSTR names a server.
/// </summary>
internal sealed class ReferenceFileStore : IAsyncDisposable
{
    internal const string Sqlite = "sqlite";
    internal const string SqlServer = "sqlserver";
    private const string SqlServerVariable = "AGENTSMITH_TEST_DB_CONNSTR";

    private readonly string? _sqlitePath;
    private readonly string? _sqlServer;

    private ReferenceFileStore(string? sqlitePath, string? sqlServer)
    {
        _sqlitePath = sqlitePath;
        _sqlServer = sqlServer;
    }

    internal static TheoryData<string> Providers()
    {
        var providers = new TheoryData<string> { Sqlite };
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(SqlServerVariable))) providers.Add(SqlServer);
        return providers;
    }

    internal static async Task<ReferenceFileStore> OpenAsync(string provider)
    {
        if (provider == Sqlite)
        {
            var path = Path.Combine(Path.GetTempPath(), $"refstore-{Guid.NewGuid():N}.db");
            MigratedStoreTemplate.CopyToFile(path);
            return new ReferenceFileStore(path, null);
        }

        await using var scratch = await ScratchSqlServer.MigratedAsync(
            Environment.GetEnvironmentVariable(SqlServerVariable)!, "refs");
        return new ReferenceFileStore(null, scratch.Database.GetConnectionString());
    }

    internal IUniqueViolationTranslator Translator =>
        _sqlServer is null ? new SqliteUniqueViolationTranslator() : new SqlServerUniqueViolationTranslator();

    internal AgentSmithDbContext Context(params IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<AgentSmithDbContext>();
        if (_sqlServer is null) builder.UseSqlite($"Data Source={_sqlitePath};Pooling=False");
        else builder.UseSqlServer(_sqlServer);
        if (interceptors.Length > 0) builder.AddInterceptors(interceptors);
        return new AgentSmithDbContext(builder.Options);
    }

    internal LegacyAttachmentCopy Copy(AgentSmithDbContext db) =>
        new(db, Translator,
            new IdentityInsertSwitch(new GeneratedKeyProperty(), NullLogger<IdentityInsertSwitch>.Instance),
            NullLogger<LegacyAttachmentCopy>.Instance);

    /// <summary>A legacy image row as an older replica wrote it, with a moment of its own.</summary>
    internal async Task<long> AddLegacyAsync(string sessionId, byte[] bytes, DateTimeOffset at)
    {
        await using var db = Context();
        var row = new SpecDialogAttachment
        {
            SessionId = sessionId, MediaType = "image/png", ContentBase64 = Convert.ToBase64String(bytes),
            CreatedAt = at, UpdatedAt = at,
        };
        using (db.SuspendAuditStamping())
        {
            db.Add(row);
            await db.SaveChangesAsync();
        }
        return row.Id;
    }

    public async ValueTask DisposeAsync()
    {
        if (_sqlServer is not null)
        {
            await using var db = Context();
            await db.Database.EnsureDeletedAsync();
        }
        else if (_sqlitePath is not null && File.Exists(_sqlitePath)) File.Delete(_sqlitePath);
    }
}
