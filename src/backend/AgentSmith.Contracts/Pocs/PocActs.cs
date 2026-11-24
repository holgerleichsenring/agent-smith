namespace AgentSmith.Contracts.Pocs;

/// <summary>
/// 2026-11-23-933da: what a PoC audit entry records. Each act is written only by the run event projection of the
/// slice that adds it (933db imported, 933dc overruled, the c81e series the rest), unique per run and act.
/// </summary>
public static class PocActs
{
    public const string Imported = "imported";
    public const string Overruled = "overruled";
    public const string Deployed = "deployed";
    public const string Stopped = "stopped";
    public const string Started = "started";
    public const string RolledBack = "rolled-back";
    public const string Deleted = "deleted";
    public const string Extended = "extended";
    public const string Expired = "expired";

    /// <summary>Every act.</summary>
    public static IReadOnlyList<string> All { get; } =
        [Imported, Overruled, Deployed, Stopped, Started, RolledBack, Deleted, Extended, Expired];
}
