namespace AgentSmith.Contracts.Persistence;

/// <summary>
/// The rendered markdown a run leaves for the dashboard: result, plan, spec and analyze.
/// The server keeps them in the database, the CLI and tests in memory. 2026-10-02-5ab2f:
/// the transient plan/diff/bootstrap slots and their promote/clear had no caller and went.
/// </summary>
public interface IRunArtifactStore
{
    /// <summary>
    /// Stores the rendered result.md so the dashboard can read it server-side
    /// (the on-disk write inside the sandbox / target repo is not reachable
    /// from the server). Operators read it AFTER WriteRunResult, not during the pipeline.
    /// </summary>
    Task WriteResultMarkdownAsync(string runId, string resultMd, CancellationToken cancellationToken);
    Task<string?> ReadResultMarkdownAsync(string runId, CancellationToken cancellationToken);

    /// <summary>
    /// p0235: caches the run's plan.md so the dashboard can show it alongside
    /// result.md. For coding presets the plan is the agent's own
    /// <c>&lt;repo&gt;/.agentsmith/plan.md</c> (read back from the sandbox at
    /// run-finish); for structured presets it is the rendered plan.
    /// </summary>
    Task WritePlanMarkdownAsync(string runId, string planMd, CancellationToken cancellationToken);
    Task<string?> ReadPlanMarkdownAsync(string runId, CancellationToken cancellationToken);

    /// <summary>p0390: the rendered work spec + its revision list. The CONTENT of record
    /// lives in git on the ticket branch — this slot is the viewer's copy, written when the
    /// revision is committed, exactly as plan.md is cached for the Plan beat.</summary>
    Task WriteSpecMarkdownAsync(string runId, string specMd, CancellationToken cancellationToken);
    Task<string?> ReadSpecMarkdownAsync(string runId, CancellationToken cancellationToken);

    /// <summary>
    /// p0243: caches the analyzer's output (the ProjectMap rendered as markdown —
    /// per-repo language, modules, test projects, build/test commands) so the
    /// dashboard can show WHAT the analyze step understood, right after it runs.
    /// Without it the analyzer's view lived only in the ephemeral sandbox and
    /// died with it — the operator flew blind on the agent's intent.
    /// </summary>
    Task WriteAnalyzeMarkdownAsync(string runId, string analyzeMd, CancellationToken cancellationToken);
    Task<string?> ReadAnalyzeMarkdownAsync(string runId, CancellationToken cancellationToken);
}
