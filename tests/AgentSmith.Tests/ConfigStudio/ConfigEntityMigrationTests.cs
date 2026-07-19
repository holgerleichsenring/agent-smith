using AgentSmith.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// p0349: the config schema is the entity-document store and nothing else. p0345's
/// relational per-entity config tables + its ConfigChange audit were a schema+
/// skeleton that NEVER shipped as an EF migration (the executor verified: no
/// migration ever created agents/trackers/config_changes; the audit was in-memory).
/// So there is nothing to DROP — this test asserts the migrated schema has the
/// entity-document tables and NO relational config tables / second audit path.
/// </summary>
public sealed class ConfigEntityMigrationTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public ConfigEntityMigrationTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using var ctx = NewContext();
        ctx.Database.Migrate();
    }

    [Fact]
    public void Migration_DropsP0345RelationalConfigTablesAndOldAudit()
    {
        var tables = TableNames();

        tables.Should().Contain(["ConfigEntities", "ConfigEntityVersions", "ConfigRefs"],
            "the entity-document store is the whole config schema");
        tables.Should().NotContain("config_changes",
            "config_entity_version is the SINGLE audit — no p0345 ConfigChange audit table exists");
        tables.Should().NotContain(new[] { "agents", "trackers", "connections", "project_repos" },
            "p0345's relational per-entity config tables were never migrated, so none exist to drop");
    }

    [Fact]
    public void ConfigEntityVersion_IsTheOnlyConfigAuditTable()
    {
        TableNames().Where(t => t.Contains("Config", StringComparison.OrdinalIgnoreCase)
                                && t.Contains("Version", StringComparison.OrdinalIgnoreCase))
            .Should().ContainSingle().Which.Should().Be("ConfigEntityVersions");
    }

    private List<string> TableNames()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table';";
        using var reader = cmd.ExecuteReader();
        var names = new List<string>();
        while (reader.Read()) names.Add(reader.GetString(0));
        return names;
    }

    private AgentSmithDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}
