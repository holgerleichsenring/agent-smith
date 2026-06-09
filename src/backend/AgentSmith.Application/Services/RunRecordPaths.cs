namespace AgentSmith.Application.Services;

/// <summary>
/// p0244: the single source of truth for the per-run record directory name, so
/// the coding master (which writes plan.md/decisions.md there, via the
/// {RunRecordDir} prompt variable) and WriteRunResultHandler (which writes
/// result.md there and reads the master's plan.md back) agree on the EXACT path
/// by construction. A mismatch would mean the master writes to dir A while the
/// framework reads dir B — the plan would be silently lost.
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
    /// the sandbox file tools root relative paths at /work (the repo root), the
    /// same place WriteRunResultHandler resolves <c>Repository.LocalPath</c> to.
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
}
