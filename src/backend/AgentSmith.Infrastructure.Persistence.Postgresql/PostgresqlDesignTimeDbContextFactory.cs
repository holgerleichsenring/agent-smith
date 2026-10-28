using AgentSmith.Contracts.Constants;
using AgentSmith.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AgentSmith.Infrastructure.Persistence.Postgresql;

/// <summary>
/// Design-time factory for the PostgreSQL migration set. Lives in the migrations
/// assembly so `dotnet ef` discovers it here, and is provider-fixed to npgsql so
/// no AGENTSMITH_PERSISTENCE_PROVIDER env toggle is needed to generate/inspect it.
/// The connection string only has to PARSE (migrations add/script never connect);
/// AGENTSMITH_PERSISTENCE_CONNECTION overrides it for design-time `database
/// update` against a real server. Runtime migration application goes through
/// `agentsmith database migrate` (config-driven), never this factory.
/// </summary>
public sealed class PostgresqlDesignTimeDbContextFactory : IDesignTimeDbContextFactory<AgentSmithDbContext>
{
    private const string MigrationsAssembly = "AgentSmith.Infrastructure.Persistence.Postgresql";
    private const string PlaceholderConnection = "Host=localhost;Database=agentsmith;Username=agentsmith";

    public AgentSmithDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable(PersistenceEnvKeys.Connection);
        var builder = new DbContextOptionsBuilder<AgentSmithDbContext>()
            .UseNpgsql(
                string.IsNullOrEmpty(connection) ? PlaceholderConnection : connection,
                o => o.MigrationsAssembly(MigrationsAssembly));
        return new AgentSmithDbContext(builder.Options);
    }
}
