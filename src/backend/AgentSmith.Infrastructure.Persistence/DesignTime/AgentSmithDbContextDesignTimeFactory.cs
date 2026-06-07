using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AgentSmith.Infrastructure.Persistence.DesignTime;

/// <summary>
/// Lets `dotnet ef` build the context without the app host. Reads the provider +
/// connection string from env (AGENTSMITH_PERSISTENCE_PROVIDER /
/// AGENTSMITH_PERSISTENCE_CONNECTION), defaulting to a SQLite file so a bare
/// `dotnet ef migrations add` works out of the box on any provider.
/// </summary>
public sealed class AgentSmithDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AgentSmithDbContext>
{
    public AgentSmithDbContext CreateDbContext(string[] args)
    {
        var provider = Environment.GetEnvironmentVariable("AGENTSMITH_PERSISTENCE_PROVIDER");
        var connection = Environment.GetEnvironmentVariable("AGENTSMITH_PERSISTENCE_CONNECTION");
        var options = new PersistenceOptions
        {
            Provider = Enum.TryParse<PersistenceProvider>(provider, ignoreCase: true, out var p)
                ? p : PersistenceProvider.Sqlite,
            ConnectionString = string.IsNullOrEmpty(connection)
                ? "Data Source=agentsmith.db" : connection,
        };

        // UseProvider extends the non-generic builder; call it on the generic
        // one (it mutates in place) and read the typed Options back.
        var builder = new DbContextOptionsBuilder<AgentSmithDbContext>();
        builder.UseProvider(options);
        return new AgentSmithDbContext(builder.Options);
    }
}
