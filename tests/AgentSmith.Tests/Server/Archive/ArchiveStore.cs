using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Tests.TestSupport;
using Microsoft.Data.Sqlite;

namespace AgentSmith.Tests.Server.Archive;

/// <summary>
/// 2026-08-28-3793: the two stores these cases need — a SOURCE an archive is taken from,
/// and a TARGET a booted server is pointed at — plus the rows that make each case what it
/// is: work this installation has recorded, and the bookkeeping a running server writes
/// about itself before anyone can press the button.
/// </summary>
internal static class ArchiveStore
{
    /// <summary>An agent the config store can really assemble, so a reload is observable.</summary>
    internal const string AgentId = "restored-scribe";

    private const string ConfigYaml = $"""
        agents:
          {AgentId}:
            provider: anthropic
            model: claude-sonnet-4-5
        """;

    /// <summary>A migrated database ON DISK, which is what a booted server can be given.</summary>
    internal static void Migrate(string path) => MigratedStoreTemplate.CopyToFile(path);

    /// <summary>A run this installation recorded — the one thing a restore collides with.</summary>
    internal static async Task RecordARunAsync(string path)
    {
        await using var db = Context(path, out var connection);
        db.Runs.Add(new Run { Id = "already-ran", Project = "p", Pipeline = "x", TicketId = "T" });
        await db.SaveChangesAsync();
        connection.Dispose();
    }

    /// <summary>
    /// What a live server has written about ITSELF: the caller it observed signing in, and
    /// the role mapping a boot with roles in the bootstrap block migrated into the config
    /// store. Neither is work; both are guaranteed to be there.
    /// </summary>
    internal static async Task WriteOwnBookkeepingAsync(string path)
    {
        await using var db = Context(path, out var connection);
        db.ObservedCallers.Add(new ObservedCallerEntity
        {
            Subject = "the-operator", NameClaim = "name", NameValue = "The Operator",
            RoleValues = "[]", GroupValues = "[]", FirstSeen = DateTimeOffset.UtcNow,
            LastSeen = DateTimeOffset.UtcNow,
        });
        db.ConfigEntities.Add(new ConfigEntity
        {
            EntityType = ConfigDocTypes.RoleMapping, EntityId = ConfigDocTypes.SingletonId,
            Doc = "{}", Version = 1, UpdatedBy = "bootstrap-migration",
        });
        await db.SaveChangesAsync();
        connection.Dispose();
    }

    /// <summary>The config a restored installation must end up serving, in the source store.</summary>
    internal static async Task WriteConfigAsync(AgentSmithDbContext db)
    {
        foreach (var doc in new ConfigDocumentAssembler().Decompose(
                     new RawConfigYaml().Deserialize(ConfigYaml)))
            db.ConfigEntities.Add(new ConfigEntity
            {
                EntityType = doc.Type, EntityId = doc.Id, Doc = doc.Doc,
                Version = 1, UpdatedBy = "the-installation-being-archived",
            });
        await db.SaveChangesAsync();
    }

    private static AgentSmithDbContext Context(string path, out SqliteConnection connection)
    {
        connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        return MigratedStoreTemplate.Context(connection);
    }
}
