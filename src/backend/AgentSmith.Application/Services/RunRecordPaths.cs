using System.Text.RegularExpressions;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0244: the single source of truth for the per-run record directory name, so
/// the coding master (which writes plan.md/decisions.md there, via the
/// {RunRecordDir} prompt variable) and WriteRunResultHandler (which writes
/// result.md there and reads the master's plan.md back) agree on the EXACT path
/// by construction. A slug mismatch would mean the master writes to dir A while
/// the framework reads dir B — the plan would be silently lost.
/// </summary>
public static class RunRecordPaths
{
    public const string AgentSmithDir = ".agentsmith";
    public const string RunsDir = "runs";

    /// <summary>The per-run directory leaf, e.g. "2026-06-06T21-01-20-1cb7-fix-the-login".</summary>
    public static string DirName(string runId, string? ticketTitle) =>
        string.IsNullOrWhiteSpace(ticketTitle) ? runId : $"{runId}-{GenerateSlug(ticketTitle!)}";

    /// <summary>
    /// Repo-relative run-record dir the AGENT writes into. Relative on purpose —
    /// the sandbox file tools root relative paths at /work (the repo root), the
    /// same place WriteRunResultHandler resolves <c>Repository.LocalPath</c> to.
    /// </summary>
    public static string RelativeDir(string runId, string? ticketTitle) =>
        $"{AgentSmithDir}/{RunsDir}/{DirName(runId, ticketTitle)}";

    /// <summary>
    /// p0253: the ONE definition of "this path is a run-record artifact, not a
    /// deliverable code change". Used by the keystone (real-code-change signal),
    /// the commit (hasCode), AND result.md's Changed Files — so all three agree.
    /// Matches a leading `.agentsmith` or any `.../.agentsmith/...` segment.
    /// </summary>
    public static bool IsRunRecordPath(string path) =>
        path.StartsWith(AgentSmithDir, StringComparison.Ordinal)
        || path.Contains($"/{AgentSmithDir}/", StringComparison.Ordinal);

    public static string GenerateSlug(string title)
    {
        var slug = title.ToLowerInvariant();
        slug = Regex.Replace(slug, @"[^a-z0-9\s-]", "");
        slug = Regex.Replace(slug, @"[\s]+", "-");
        slug = slug.Trim('-');
        return slug.Length > 40 ? slug[..40].TrimEnd('-') : slug;
    }
}
