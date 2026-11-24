using System.Security.Cryptography;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Core.Services.Configuration;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services.Secrets;
using AgentSmith.Tests.TestSupport;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Pocs.Variables;

/// <summary>2026-11-23-933dg: the secret store over a migrated SQLite copy with one master key — the repository, the
/// configuration load's values, the PoC values and the seal fingerprint, each as the composition builds them.</summary>
internal sealed class SecretStoreRig : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly ServiceProvider _services;

    public SecretStoreRig(string? masterKey = null)
    {
        Key = masterKey ?? Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        Keys = new MasterKeyRing(name => name == MasterKeyRing.Variable ? Key : null);
        _services = new ServiceCollection()
            .AddScoped(_ => MigratedStoreTemplate.Context(_connection))
            .AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<AgentSmithDbContext>())
            .AddScoped<PocVariableRepository>()
            .BuildServiceProvider();
    }

    public string Key { get; }

    public MasterKeyRing Keys { get; }

    public StartupFindings Findings { get; } = new();

    public AgentSmithDbContext Db() => _services.CreateScope().ServiceProvider.GetRequiredService<AgentSmithDbContext>();

    public StoredSecretRepository Vault() => new(Db(), Keys, new SecretEnvelope(), TimeProvider.System);

    public StoredSecretValues Load() => new(_services.GetRequiredService<IServiceScopeFactory>(), Keys, new SecretEnvelope(), Findings,
        NullLogger<StoredSecretValues>.Instance);

    public PocSecretValues PocValues(MasterKeyRing? keys = null) => new(_services.GetRequiredService<IServiceScopeFactory>(),
        keys ?? Keys, new SecretEnvelope(), NullLogger<PocSecretValues>.Instance);

    public AgentSmith.Infrastructure.Persistence.Services.DbPocVariableStore Variables() =>
        new(_services.GetRequiredService<IServiceScopeFactory>());

    public PocSealFingerprint Fingerprint() => new(Keys);

    public async Task<Poc> PocAsync(string name, string project)
    {
        await using var db = Db();
        return await PocRows.AddAsync(db, PocRows.Poc(name, project));
    }

    public Task SetAsync(SecretScope scope, string name, string value) =>
        Vault().SetAsync(scope, name, new SecretInput(value), "ann-sub", CancellationToken.None);

    public void Dispose()
    {
        _services.Dispose();
        _connection.Dispose();
    }
}
