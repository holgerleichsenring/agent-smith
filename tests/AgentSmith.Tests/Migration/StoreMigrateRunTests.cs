using AgentSmith.Infrastructure.Migration.Extensions;
using AgentSmith.Infrastructure.Migration.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Tests.Migration;

/// <summary>
/// 2026-11-22-747da: `database migrate` end to end on a SQLite file, the way the init container meets it — and a
/// second time, which must change nothing.
/// </summary>
[Trait("TestProcess", "env-2")]
[Collection(EnvVarCollection.Name)]
public sealed class StoreMigrateRunTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("store-migrate-").FullName;

    [Fact]
    public async Task Run_Twice_TheSecondAppliesNoDataMigration()
    {
        var store = $"Data Source={Path.Combine(_dir, "a.db")}";
        var config = Config($"persistence:\n  provider: sqlite\n  connection_string: {store}\n");

        (await RunAsync(config, new RecordingLoggerProvider())).Should().Be(0);
        (await RunAsync(config, new RecordingLoggerProvider())).Should().Be(0);
        var third = new RecordingLoggerProvider();
        (await RunAsync(config, third)).Should().Be(0);

        (await RecordedAsync(store)).Should().BeEquivalentTo(
            "2026-10-12-967ab-spec-names", "2026-11-22-a720a-kept-set-carry", "2026-08-25-1806-role-mapping-import",
            "2026-09-25-e5b1-preset-aliases", "2026-10-29-6bd3a-trigger-blocks");
        third.Entries.Should().NotContain(e => e.Message.StartsWith("Data migration", StringComparison.Ordinal),
            "every data migration was recorded by the second migrate, so the third looks at none");
    }

    [Fact]
    public async Task Run_NoConfigurationFile_Refuses()
    {
        (await RunAsync(Path.Combine(_dir, "missing.yml"), new RecordingLoggerProvider())).Should().Be(1);
    }

    [Fact]
    public async Task Run_UnknownProvider_Refuses()
    {
        var config = Config("persistence:\n  provider: oracle\n  connection_string: x\n");

        (await RunAsync(config, new RecordingLoggerProvider())).Should().Be(1);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        Directory.Delete(_dir, recursive: true);
    }

    private static async Task<List<string>> RecordedAsync(string connectionString)
    {
        await using var db = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(connectionString).Options);
        return await db.Set<DataMigrationRecord>().Select(r => r.Id).ToListAsync();
    }

    private string Config(string yaml)
    {
        var path = Path.Combine(_dir, $"{Guid.NewGuid():N}.yml");
        File.WriteAllText(path, yaml);
        return path;
    }

    private static async Task<int> RunAsync(string configPath, RecordingLoggerProvider logs)
    {
        await using var provider = new ServiceCollection().AddLogging(b => b.AddProvider(logs))
            .AddStoreMigrate(configPath).BuildServiceProvider();
        return await provider.GetRequiredService<StoreMigrateRun>().RunAsync(CancellationToken.None);
    }
}
