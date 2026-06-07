namespace AgentSmith.Infrastructure.Persistence.Models;

/// <summary>
/// The relational backend EF Core targets. Its own config field (not a
/// connection-string scheme prefix) because EF must call the matching
/// Use{Sqlite|Npgsql|MySql} regardless — explicit beats shape auto-detection.
/// </summary>
public enum PersistenceProvider
{
    Sqlite,
    Postgresql,
    Mysql,
}
