using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Tests.Architecture;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// 2026-10-28-ebb0: the PostgreSQL migration set is generated under Npgsql, so its snapshot has
/// to describe the model exactly — a model change that forgot this set shows up here, offline,
/// before any database refuses it.
/// </summary>
public sealed class PostgresqlMigrationSetTests
{
    private const string Assembly = "AgentSmith.Infrastructure.Persistence.Postgresql";

    [Fact]
    public void PostgresqlMigrationSet_HasNoPendingModelChanges()
    {
        using var db = Context();

        db.Database.HasPendingModelChanges().Should().BeFalse(
            "every model change needs a migration in the PostgreSQL set too");
    }

    [Fact]
    public void PostgresqlMigrationSet_TypesColumnsForPostgres_NotForSqlite()
    {
        var baseline = File.ReadAllText(Directory.GetFiles(
            Path.Combine(ArchitectureSources.BackendRoot, Assembly, "Migrations"), "*_AddMemoryEvidence.cs").Single());

        baseline.Should().Contain("type: \"timestamp with time zone\"").And.Contain("type: \"boolean\"")
            .And.Contain("NpgsqlValueGenerationStrategy.IdentityByDefaultColumn")
            .And.Contain("\"Npgsql:IdentitySequenceOptions\", \"'1000000000'")
            .And.NotContain("Sqlite:Autoincrement");
    }

    private static AgentSmithDbContext Context()
    {
        var builder = new DbContextOptionsBuilder<AgentSmithDbContext>();
        builder.UseProvider(new PersistenceOptions
        {
            Provider = PersistenceProvider.Postgresql,
            ConnectionString = "Host=localhost;Database=x",
        });
        return new AgentSmithDbContext(builder.Options);
    }
}
