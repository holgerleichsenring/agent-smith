namespace AgentSmith.Server.Services.Sandbox;

/// <summary>
/// p0465: whether this instance runs the Docker orphan reaper at all, and why.
/// The Docker backend is AUTO-DETECTED from /var/run/docker.sock, so before this
/// phase every dev machine and every side-instance armed a daemon-wide reaper.
/// The reaper's whole judgement rests on the liveness store answering "is this run
/// alive?", so it runs where a real <c>IActiveRunLease</c> can answer and stands
/// down where the lease is the DB-free no-op. An operator override decides either
/// way — <c>SANDBOX_ORPHAN_REAPER=true|false</c>.
/// </summary>
public sealed record SandboxReaperActivation(bool ShouldRun, string Reason)
{
    public const string OverrideEnvVar = "SANDBOX_ORPHAN_REAPER";

    internal static SandboxReaperActivation Decide(bool leaseAnswersLiveness, string? operatorOverride)
    {
        if (bool.TryParse(operatorOverride?.Trim(), out var forced))
            return new SandboxReaperActivation(forced, $"{OverrideEnvVar}={forced}");
        return leaseAnswersLiveness
            ? new SandboxReaperActivation(true, "a durable active-run lease can report live runs")
            : new SandboxReaperActivation(false,
                "no durable active-run lease is registered (persistence is off), so live runs "
                + "cannot be told from dead ones");
    }
}
