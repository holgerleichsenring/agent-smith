namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-02-075da: one entry an upload did not store, and why — a rebuildable folder (collapsed
/// to the folder) or a file over the per-file bound.
/// </summary>
public sealed record ReferenceLeftOut(string Path, string Reason)
{
    public const string Rebuildable = "rebuildable";

    public const string TooLarge = "too large";
}
