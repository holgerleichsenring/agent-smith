using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Models;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// Guards the provider switch in <see cref="PersistenceOptionsExtensions.UseProvider"/>.
/// SQL Server can't share the SQLite-generated migration set (baked TEXT column
/// types), so it must resolve its dedicated migrations assembly — this asserts
/// that wiring so a refactor can't silently drop it and send `database migrate`
/// back to the shared set.
/// </summary>
public sealed class ProviderWiringTests
{
    [Theory]
    [InlineData(PersistenceProvider.Sqlite, "Microsoft.EntityFrameworkCore.Sqlite")]
    [InlineData(PersistenceProvider.Postgresql, "Npgsql.EntityFrameworkCore.PostgreSQL")]
    [InlineData(PersistenceProvider.Mysql, "Pomelo.EntityFrameworkCore.MySql")]
    [InlineData(PersistenceProvider.SqlServer, "Microsoft.EntityFrameworkCore.SqlServer")]
    public void UseProvider_SelectsMatchingProvider(PersistenceProvider provider, string expectedProviderName)
    {
        using var ctx = BuildContext(provider);

        ctx.Database.ProviderName.Should().Be(expectedProviderName);
    }

    [Fact]
    public void UseProvider_SqlServer_TargetsDedicatedMigrationsAssembly()
    {
        using var ctx = BuildContext(PersistenceProvider.SqlServer);

        var migrationsAssembly = ctx.GetService<IMigrationsAssembly>().Assembly.GetName().Name;

        migrationsAssembly.Should().Be(
            "AgentSmith.Infrastructure.Persistence.SqlServer",
            "SQL Server must apply its own migration set, not the shared SQLite-typed one");
    }

    [Fact]
    public void UseProvider_Sqlite_UsesTheSharedMigrationsAssembly()
    {
        using var ctx = BuildContext(PersistenceProvider.Sqlite);

        var migrationsAssembly = ctx.GetService<IMigrationsAssembly>().Assembly.GetName().Name;

        migrationsAssembly.Should().Be(
            "AgentSmith.Infrastructure.Persistence",
            "the default providers share the migration set that lives beside the DbContext");
    }

    // A syntactically valid connection string per provider — nothing connects
    // (ProviderName + migrations assembly are resolved from options alone).
    private static AgentSmithDbContext BuildContext(PersistenceProvider provider)
    {
        var options = new PersistenceOptions
        {
            Provider = provider,
            ConnectionString = provider switch
            {
                PersistenceProvider.Sqlite => "Data Source=:memory:",
                PersistenceProvider.Postgresql => "Host=localhost;Database=x;Username=x;Password=x",
                PersistenceProvider.Mysql => "Server=localhost;Database=x;User=x;Password=x",
                PersistenceProvider.SqlServer => "Server=localhost;Database=x;TrustServerCertificate=True",
                _ => throw new ArgumentOutOfRangeException(nameof(provider)),
            },
        };

        var builder = new DbContextOptionsBuilder<AgentSmithDbContext>();
        builder.UseProvider(options);
        return new AgentSmithDbContext(builder.Options);
    }
}
