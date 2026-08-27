namespace AgentSmith.Server.Services.Diagnostics;

/// <summary>
/// 2026-08-27-729e: what the persistence probe actually learned, before it is folded into
/// a reachability verdict. The pending-migration count was computed here and then spent on
/// a status sentence, which is why "did my migration run" was answered by reading container
/// logs. Reporting it costs a projection; counting it a second time would cost a second
/// query that could disagree with the first.
/// </summary>
/// <param name="Reachable">Whether the database answered at all.</param>
/// <param name="PendingMigrations">Migrations the schema is missing; 0 when it is current.</param>
/// <param name="Error">Why it could not be read, or null when it was read.</param>
public sealed record PersistenceState(bool Reachable, int PendingMigrations, string? Error)
{
    public static PersistenceState Unreachable(string error) => new(false, 0, error);
}
