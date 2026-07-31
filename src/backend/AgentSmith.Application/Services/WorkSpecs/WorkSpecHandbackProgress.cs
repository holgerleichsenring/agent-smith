using AgentSmith.Contracts.WorkSpecs;

namespace AgentSmith.Application.Services.WorkSpecs;

/// <summary>
/// p0390: non-progress across runs is MECHANICAL, never a prose comparison —
/// two hand-backs with the same CASE CODE and no source commit on the ticket
/// branch between them end the loop. Comparing LLM-written reasons would never
/// match: the same fact is written differently twice.
/// </summary>
public static class WorkSpecHandbackProgress
{
    /// <summary>True when this hand-back repeats the previous one with nothing done between.</summary>
    public static bool RepeatsWithoutProgress(
        WorkSpecPointer? pointer, WorkSpecHandbackCase current, string branchHeadSha)
    {
        if (pointer is null || pointer.LastHandbackCase != current) return false;
        if (string.IsNullOrEmpty(pointer.HandbackSourceSha)) return false;
        return string.Equals(pointer.HandbackSourceSha, branchHeadSha, StringComparison.Ordinal);
    }

    /// <summary>The pointer to record after a hand-back was posted.</summary>
    public static WorkSpecPointer Record(
        WorkSpecPointer pointer, WorkSpecHandbackCase current, string branchHeadSha) =>
        pointer with
        {
            LastHandbackCase = current,
            RepeatedHandbackCount = pointer.LastHandbackCase == current
                ? pointer.RepeatedHandbackCount + 1
                : 1,
            HandbackSourceSha = branchHeadSha,
        };
}
