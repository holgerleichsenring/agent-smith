using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Persistence.Models;

namespace AgentSmith.Cli.Services;

/// <summary>
/// p0423: decides where a CLI run writes its record.
/// <para>
/// The configured default points at <c>/var/lib/agentsmith/agentsmith.db</c> — the
/// container path the server runs on, and a path a developer's machine will not let the
/// CLI create. That is how twelve hours of live debugging ran against no record at all:
/// nothing was misconfigured, the destination simply did not exist and nobody was told.
/// </para>
/// <para>
/// So: use what the configuration says whenever that location can be written, and
/// otherwise fall back to a per-user store beside the rest of agent-smith's local state.
/// The fallback is announced, never silent — a record in a place nobody knows about is
/// the same as no record.
/// </para>
/// </summary>
public static class CliRunStoreLocation
{
    public static PersistenceOptions Resolve(PersistenceConfig config, out string? fellBackTo)
    {
        fellBackTo = null;
        var provider = Enum.TryParse<PersistenceProvider>(config.Provider, ignoreCase: true, out var p)
            ? p
            : PersistenceProvider.Sqlite;

        if (provider != PersistenceProvider.Sqlite || IsWritable(config.ConnectionString))
            return new PersistenceOptions { Provider = provider, ConnectionString = config.ConnectionString };

        fellBackTo = LocalStorePath();
        return new PersistenceOptions
        {
            Provider = PersistenceProvider.Sqlite,
            ConnectionString = $"Data Source={fellBackTo}",
        };
    }

    private static bool IsWritable(string connectionString)
    {
        var path = DataSourceOf(connectionString);
        if (string.IsNullOrWhiteSpace(path)) return false;
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (string.IsNullOrEmpty(directory)) return false;
            Directory.CreateDirectory(directory);
            return true;
        }
        catch { return false; }
    }

    private static string? DataSourceOf(string connectionString) => connectionString
        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(part => part.Split('=', 2))
        .Where(kv => kv.Length == 2 && kv[0].Trim().Equals("Data Source", StringComparison.OrdinalIgnoreCase))
        .Select(kv => kv[1].Trim())
        .FirstOrDefault();

    private static string LocalStorePath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var directory = Path.Combine(home, ".agentsmith");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, "runs.db");
    }
}
