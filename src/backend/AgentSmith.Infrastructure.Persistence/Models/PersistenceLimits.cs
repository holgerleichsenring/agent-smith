namespace AgentSmith.Infrastructure.Persistence.Models;

/// <summary>
/// Column-length limits that load-bearing indexes depend on. An indexed string
/// column MUST cap at 191 chars or the MySQL utf8mb4 index-key-length limit
/// (767 bytes / 4 bytes-per-char) fails the migration.
/// </summary>
public static class PersistenceLimits
{
    public const int IndexedString = 191;
}
