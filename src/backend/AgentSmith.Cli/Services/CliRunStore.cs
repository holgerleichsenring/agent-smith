using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Persistence.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Cli.Services;

/// <summary>
/// p0423: where this CLI process records its runs, resolved once and announced.
/// <see cref="CliRunStoreLocation"/> decides; this holds the decision so the DbContext,
/// the schema step and the operator all read the same answer.
/// </summary>
public sealed class CliRunStore
{
    private readonly string? _fellBackTo;

    public CliRunStore(AgentSmithConfig config, ILogger<CliRunStore> logger)
    {
        Options = CliRunStoreLocation.Resolve(config.Persistence, out _fellBackTo);
        if (_fellBackTo is not null)
            logger.LogInformation("Recording this run to {Path} — the configured store is not writable here.", _fellBackTo);
    }

    public PersistenceOptions Options { get; }

    /// <summary>
    /// True when the store is a SQLite file this one-shot process opens by itself, so it
    /// may bring the schema up to date. A shared provider is assumed current, as on the
    /// server, where <c>agentsmith database migrate</c> owns that step.
    /// </summary>
    public bool IsLocalSqlite => Options.Provider == PersistenceProvider.Sqlite;
}
