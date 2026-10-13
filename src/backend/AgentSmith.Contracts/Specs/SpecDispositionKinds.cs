namespace AgentSmith.Contracts.Specs;

/// <summary>
/// 2026-10-13-9a59l: what an approval does to one spec of a worked series. An executed spec is always
/// kept; an unstarted one is re-cut, kept or dropped; a spec the series did not hold is appended.
/// </summary>
public static class SpecDispositionKinds
{
    public const string Kept = "kept";
    public const string Recut = "recut";
    public const string Appended = "appended";
    public const string Dropped = "dropped";
}
