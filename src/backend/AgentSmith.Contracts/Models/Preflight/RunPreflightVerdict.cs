namespace AgentSmith.Contracts.Models.Preflight;

/// <summary>
/// p0428: what one per-run preflight check concluded.
/// <para>
/// <see cref="Warn"/> exists so a finding can be REPORTED without refusing the run.
/// A preflight that fails a healthy run is worse than no preflight, so anything the
/// framework itself legitimately produces — a ticket branch carrying an earlier run's
/// checkpoint commits, say — is a warning by construction.
/// </para>
/// </summary>
public enum RunPreflightVerdict
{
    Pass = 0,
    Warn = 1,
    Fail = 2,
}
