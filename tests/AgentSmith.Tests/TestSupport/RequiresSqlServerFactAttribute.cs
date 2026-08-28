namespace AgentSmith.Tests.TestSupport;

/// <summary>
/// 2026-08-28-2af6: a fact that runs only when a SQL Server is handed to the suite, and
/// reports itself as SKIPPED WITH A REASON when one is not.
/// <para>
/// A test that quietly returns when its database is absent lets the claim it exists to
/// prove pass unproven; the runner has to say out loud that it did not run.
/// </para>
/// </summary>
public sealed class RequiresSqlServerFactAttribute : FactAttribute
{
    /// <summary>The connection string the suite already uses for its SQL Server legs.</summary>
    public const string ConnectionStringVariable = "AGENTSMITH_TEST_DB_CONNSTR";

    public RequiresSqlServerFactAttribute()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString)) return;
        Skip = $"NOT RUN: set {ConnectionStringVariable} to a SQL Server connection string "
            + "(with permission to create a database) to prove the cross-provider leg.";
    }

    internal static string? ConnectionString =>
        Environment.GetEnvironmentVariable(ConnectionStringVariable);
}
