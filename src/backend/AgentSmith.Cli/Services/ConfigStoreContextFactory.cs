using AgentSmith.Cli;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Cli.Services;

/// <summary>
/// The database context `agentsmith config export|import` reads and writes: the persistence
/// block of the named configuration, opened the same deliberate way `database migrate` does.
/// </summary>
internal sealed class ConfigStoreContextFactory
{
    public AgentSmithDbContext Create(string configPath, bool verbose)
    {
        using var services = ServiceProviderFactory.Build(configPath, verbose, headless: true);
        var persistence = services.GetRequiredService<IConfigurationLoader>().LoadConfig(configPath).Persistence;
        var provider = Enum.TryParse<PersistenceProvider>(persistence.Provider, ignoreCase: true, out var p)
            ? p : PersistenceProvider.Sqlite;
        var options = new PersistenceOptions { Provider = provider, ConnectionString = persistence.ConnectionString };
        var builder = new DbContextOptionsBuilder<AgentSmithDbContext>();
        builder.UseProvider(options);
        if (provider != PersistenceProvider.Sqlite)
            builder.ConfigureWarnings(w =>
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        return new AgentSmithDbContext(builder.Options);
    }
}
