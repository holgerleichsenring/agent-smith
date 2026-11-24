using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-11-23-933dg: StoredSecrets gains Scope and its key moves to (Scope, Name) — on SQLite a table rebuild. A row
/// stored before reads as the installation's with its name and bytes unchanged; every provider names the new key.
/// </summary>
[Collection(RelationalStoreCollection.Name)]
public sealed class StoredSecretScopeMigrationTests : IDisposable
{
    private const string MigrationBefore = "AddPocBuild";
    private const string Migration = "AddPocVariables";
    private const string Stamp = "'2026-11-01 00:00:00+00:00'";

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    public StoredSecretScopeMigrationTests() => _connection.Open();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task ScopeMigration_KeepsAnInstallationRow_AsTheInstallations()
    {
        await using var db = new AgentSmithDbContext(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(MigrationBefore);
        await db.Database.ExecuteSqlRawAsync("INSERT INTO StoredSecrets (Name, Version, Ciphertext, Nonce, WrappedKey, WrapNonce, KeyId, "
            + $"SetBy, SetAt, CreatedAt, UpdatedAt) VALUES ('github_token', 1, x'0102', x'03', x'04', x'05', 'k1', 'ada', {Stamp}, {Stamp}, {Stamp})");

        await migrator.MigrateAsync(Migration);

        var row = await db.Set<StoredSecret>().AsNoTracking().SingleAsync();
        (row.Scope, row.Name, row.KeyId, row.SetBy).Should().Be(("installation", "github_token", "k1", "ada"));
        row.Ciphertext.Should().Equal(1, 2);
    }

    [Fact]
    public void ScopeKey_IsScopeAndName_OnEveryProvider()
    {
        foreach (var provider in new[] { PersistenceProvider.Sqlite, PersistenceProvider.SqlServer, PersistenceProvider.Postgresql })
        {
            var builder = new DbContextOptionsBuilder<AgentSmithDbContext>();
            builder.UseProvider(new PersistenceOptions { Provider = provider, ConnectionString = ConnectionFor(provider) });
            using var db = new AgentSmithDbContext(builder.Options);
            db.Database.GetMigrations().Select(id => id.Split('_', 2).Last()).Should().ContainSingle(n => n == Migration);
            db.Model.FindEntityType(typeof(StoredSecret))!.FindPrimaryKey()!.Properties.Select(p => p.Name)
                .Should().Equal("Scope", "Name");
        }
    }

    private static string ConnectionFor(PersistenceProvider provider) => provider switch
    {
        PersistenceProvider.SqlServer => "Server=localhost;Database=x;Trusted_Connection=True;TrustServerCertificate=True",
        PersistenceProvider.Postgresql => "Host=localhost;Database=x;Username=x;Password=x",
        _ => "Data Source=:memory:",
    };
}
