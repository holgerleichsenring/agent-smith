using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Models;
using AgentSmith.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AgentSmith.Cli.Services;

/// <summary>
/// 2026-08-28-2af6: opens a context on the store the config file names — the same way
/// `database migrate` and `config export` reach a database, and for the same reason: the
/// case a store copy exists for is an installation whose server is being replaced or will
/// not start, and the connection string carries credentials that must stay off the command
/// line and out of the shell history.
/// </summary>
internal sealed class ConfiguredStoreFactory(IConfigurationLoader loader)
{
    public AgentSmithDbContext Create(string configPath)
    {
        var persistence = loader.LoadConfig(configPath).Persistence;
        if (!Enum.TryParse<PersistenceProvider>(persistence.Provider, ignoreCase: true, out var provider))
            throw new ConfigurationException(
                $"Unknown persistence.provider '{persistence.Provider}' "
                + "(expected sqlite | postgresql | mysql | sqlserver).");

        var builder = new DbContextOptionsBuilder<AgentSmithDbContext>();
        builder.UseProvider(new PersistenceOptions
        {
            Provider = provider,
            ConnectionString = persistence.ConnectionString,
        });
        // The shared model snapshot is generated under SQLite; under any other provider the
        // runtime model gains annotations it lacks, which EF 9 misreads as pending changes.
        if (provider != PersistenceProvider.Sqlite)
            builder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        return new AgentSmithDbContext(builder.Options);
    }
}
