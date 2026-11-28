namespace AgentSmith.Application.Services;

/// <summary>
/// p0244: the per-run record directory a run used to write under <c>.agentsmith/runs/</c>. 2026-11-28-db88b: no run
/// writes plan.md or result.md there any more — the record lives with the run — but repositories keep the
/// directories earlier runs committed, and a run started before the change may still carry one, so the path
/// checks below stay.
/// </summary>
public static class RunRecordPaths
{
    public const string AgentSmithDir = ".agentsmith";
    public const string RunsDir = "runs";

    /// <summary>
    /// The per-run directory leaf — the run id ALONE, e.g.
    /// "2026-06-08T14-46-27-40fa". p0258: the ticket-title slug was dropped from
    /// the folder name; the description belongs INSIDE plan.md / result.md, not in
    /// a path segment (the slug made the dir name long and is redundant with the
    /// run id, which is the stable key everything else joins on).
    /// </summary>
    public static string DirName(string runId) => runId;

    /// <summary>
    /// Repo-relative run-record dir the AGENT writes into. Relative on purpose —
    /// the sandbox file tools root relative paths at the repository root (/work/&lt;repo&gt;),
    /// the same place WriteRunResultHandler's relative paths resolve to.
    /// </summary>
    public static string RelativeDir(string runId) =>
        $"{AgentSmithDir}/{RunsDir}/{DirName(runId)}";

    /// <summary>
    /// p0253: the ONE definition of "this path is a run-record artifact, not a
    /// deliverable code change". Used by the keystone (real-code-change signal),
    /// the commit (hasCode), AND result.md's Changed Files — so all three agree.
    /// Matches a leading `.agentsmith` or any `.../.agentsmith/...` segment.
    /// </summary>
    public static bool IsRunRecordPath(string path) =>
        path.StartsWith(AgentSmithDir, StringComparison.Ordinal)
        || path.Contains($"/{AgentSmithDir}/", StringComparison.Ordinal);

    /// <summary>
    /// p0322c: narrower than <see cref="IsRunRecordPath"/> — ONLY the per-run
    /// record under <c>.agentsmith/runs/</c>. Init's deliverables (context.yaml,
    /// principles.md) live directly in <c>.agentsmith/</c> too, so the
    /// broad check would misclassify a real init diff as record-only.
    /// </summary>
    public static bool IsUnderRunsDir(string path) =>
        path.StartsWith($"{AgentSmithDir}/{RunsDir}/", StringComparison.Ordinal);
}
