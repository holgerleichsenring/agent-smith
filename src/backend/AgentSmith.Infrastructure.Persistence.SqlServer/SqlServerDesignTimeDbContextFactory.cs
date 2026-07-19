using AgentSmith.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AgentSmith.Infrastructure.Persistence.SqlServer;

/// <summary>
/// Design-time factory for the SQL Server migration set. Lives in the migrations
/// assembly so `dotnet ef` discovers it here (EF scans the --project assembly),
/// and is provider-fixed to sqlserver — the whole point of this assembly — so no
/// AGENTSMITH_PERSISTENCE_PROVIDER env toggle is needed to generate/inspect it.
/// The connection string only has to PARSE (migrations add/script never connect);
/// AGENTSMITH_PERSISTENCE_CONNECTION overrides it for design-time `database
/// update` against a real server. Runtime migration application goes through
/// `agentsmith database migrate` (config-driven), never this factory.
/// </summary>
public sealed class SqlServerDesignTimeDbContextFactory : IDesignTimeDbContextFactory<AgentSmithDbContext>
{
    private const string MigrationsAssembly = "AgentSmith.Infrastructure.Persistence.SqlServer";
    private const string PlaceholderConnection =
        "Server=localhost;Database=agentsmith;Trusted_Connection=False;TrustServerCertificate=True";

    public AgentSmithDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("AGENTSMITH_PERSISTENCE_CONNECTION");
        var builder = new DbContextOptionsBuilder<AgentSmithDbContext>()
            .UseSqlServer(
                string.IsNullOrEmpty(connection) ? PlaceholderConnection : connection,
                o => o.MigrationsAssembly(MigrationsAssembly));
        return new AgentSmithDbContext(builder.Options);
    }
}
