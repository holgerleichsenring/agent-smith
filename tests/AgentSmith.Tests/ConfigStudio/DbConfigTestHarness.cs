using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Core.Services.Configuration.Studio;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.ConfigStudio;

/// <summary>
/// p0349: spins up the full DB config stack on a real, migrated SQLite in-memory
/// database with foreign keys ENFORCED (so config_ref RESTRICT is genuinely tested).
/// Mirrors the server wiring: scoped repositories, the singleton doc-store facade,
/// DbConfigStore over the type&lt;-&gt;model assembler.
/// </summary>
public sealed class DbConfigTestHarness : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    public DbConfigTestHarness()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        EnableForeignKeys();

        var services = new ServiceCollection();
        services.AddDbContext<AgentSmithDbContext>(b => b.UseSqlite(_connection), ServiceLifetime.Scoped);
        services.AddScoped<ConfigDocumentRepository>();
        services.AddScoped<ConfigImportRepository>();
        services.AddSingleton<ConfigDocumentAssembler>();
        services.AddSingleton<IConfigDocumentStore, EfConfigDocumentStore>();
        services.AddSingleton<IConfigStore, DbConfigStore>();
        _provider = services.BuildServiceProvider();

        using var scope = _provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<AgentSmithDbContext>().Database.Migrate();
    }

    public IConfigDocumentStore DocStore => _provider.GetRequiredService<IConfigDocumentStore>();
    public IConfigStore Store => _provider.GetRequiredService<IConfigStore>();
    public ConfigDocumentAssembler Assembler => _provider.GetRequiredService<ConfigDocumentAssembler>();

    public void Import(string yaml, bool force = false)
    {
        var raw = RawConfigYaml.Deserialize(yaml);
        var writes = Assembler.Decompose(raw)
            .Select(d => new ConfigDocWrite(d.Type, d.Id, d.Doc, null, d.Edges, "importer"))
            .ToList();
        DocStore.Import(writes, force);
        Store.Load();
    }

    public static AgentSmith.Infrastructure.Core.Services.Configuration.YamlConfigurationLoader RealLoader() =>
        new(new AgentSmith.Infrastructure.Core.Services.Configuration.RawConfigMaterializer(
                new AgentSmith.Infrastructure.Core.Services.Configuration.ProjectConfigNormalizer(),
                new AgentSmith.Infrastructure.Core.Services.Configuration.EffectiveTriggerBuilder(),
                new AgentSmith.Infrastructure.Core.Services.Configuration.DeploymentDefaultsApplier(),
                new AgentSmith.Infrastructure.Core.Services.Configuration.ConfigCatalogResolver(),
                new AgentSmith.Infrastructure.Core.Services.AgentSmithPaths()),
            new AgentSmith.Application.Services.Events.NoOpSystemEventPublisher());

    private void EnableForeignKeys()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "PRAGMA foreign_keys=ON;";
        cmd.ExecuteNonQuery();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }
}
