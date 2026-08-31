namespace AgentSmith.Contracts.Models;

/// <summary>
/// 2026-08-30-26ed: the words a dynamic scan uses to say its coverage is smaller than
/// its finding count suggests. One place, because the run record, the result document
/// and the findings account all have to say the same thing about the same run.
/// </summary>
public static class ScanDegradation
{
    /// <summary>
    /// A scan the runner stopped at its own time limit. Its findings cover the part of
    /// the target it reached, so an empty result says nothing about the rest.
    /// </summary>
    public static string CutOffAt(int limitSeconds) =>
        $"cut off at its {limitSeconds}s time limit before it finished";

    /// <summary>Joins the reasons a scan is degraded, or null when it is not.</summary>
    public static string? Combine(params string?[] reasons)
    {
        var stated = reasons.Where(r => !string.IsNullOrWhiteSpace(r)).ToArray();
        return stated.Length == 0 ? null : string.Join("; ", stated);
    }
}
