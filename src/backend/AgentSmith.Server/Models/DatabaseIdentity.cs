namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-08-27-729e: which store this installation records runs in, and whether its schema
/// is current. Both decide whether a run can be recorded at all, and both were previously
/// answerable only by reading container logs.
/// </summary>
public sealed record DatabaseIdentity(
    string Provider,
    bool Reachable,
    int PendingMigrations,
    string? Error);
