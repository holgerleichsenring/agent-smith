namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// p0391a: how much of the installation a <see cref="StartupFinding"/> takes out.
/// Blocking = the unit the finding names does not run; Advisory = it runs, but the
/// operator should know. Neither severity ever stops the process.
/// </summary>
public enum StartupFindingSeverity
{
    Advisory,
    Blocking,
}
