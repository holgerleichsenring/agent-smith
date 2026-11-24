namespace AgentSmith.Contracts.Pocs;

/// <summary>
/// 2026-11-23-933da: the states a PoC row takes — the full set, written by later slices: importing and refused
/// (933db), blocked (933dc), ready and failed (c81ec), stopped and deleted (c81ed). <see cref="IsLive"/> is the one
/// definition of a live PoC, read by the per-person limit and the name check.
/// </summary>
public static class PocStates
{
    public const string Importing = "importing";
    public const string Refused = "refused";
    public const string Blocked = "blocked";
    public const string Ready = "ready";
    public const string Failed = "failed";
    public const string Stopped = "stopped";
    public const string Deleted = "deleted";

    /// <summary>Every state, in lifecycle order.</summary>
    public static IReadOnlyList<string> All { get; } = [Importing, Refused, Blocked, Ready, Failed, Stopped, Deleted];

    /// <summary>A PoC holds its name and a per-person slot unless its import was refused or it was deleted; a blocked
    /// PoC that never deployed stays live until c81ee removes it with its purged upload.</summary>
    public static bool IsLive(string state) => state is not (Refused or Deleted);
}
