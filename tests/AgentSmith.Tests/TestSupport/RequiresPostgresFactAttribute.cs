namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-10-28-ebb0: a fact that runs only when a PostgreSQL server is handed to the suite, and
/// reports itself as SKIPPED WITH A REASON when one is not — the same contract as
/// <see cref="RequiresSqlServerFactAttribute"/>.
/// </summary>
public sealed class RequiresPostgresFactAttribute : FactAttribute
{
    /// <summary>A PostgreSQL connection string with permission to create a database.</summary>
    public const string ConnectionStringVariable = "AGENTSMITH_TEST_PG_CONNSTR";

    public RequiresPostgresFactAttribute()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString)) return;
        Skip = $"NOT RUN: set {ConnectionStringVariable} to a PostgreSQL connection string "
            + "(with permission to create a database) to prove the PostgreSQL leg.";
    }

    internal static string? ConnectionString =>
        Environment.GetEnvironmentVariable(ConnectionStringVariable);
}
