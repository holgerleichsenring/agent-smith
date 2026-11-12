namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-11-05-862ea: how long the stored picture may be once serialized (escaping included) — the column's width.
/// In Contracts because the tool's admission and the column read the same number. 2026-11-12-cd4cd: the truth has
/// no bound of its own.
/// </summary>
public static class WorkPictureLimits
{
    public const int Stored = 2000;
}
