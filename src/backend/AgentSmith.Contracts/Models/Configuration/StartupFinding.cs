namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>
/// p0391a: one thing that is wrong with this installation, in operator language.
/// It names the narrowest broken unit (a subsystem, and where applicable the project,
/// trigger and configuration field), how badly it is broken, and why. One producer,
/// three readers: boot decides what to disable, the findings endpoint answers "what is
/// wrong with my installation", and the dashboard renders it.
/// </summary>
public sealed record StartupFinding(
    string Subsystem,
    StartupFindingSeverity Severity,
    string Reason,
    string? Project = null,
    string? Trigger = null,
    string? Field = null)
{
    /// <summary>
    /// The unit this finding is about. Two findings with the same identity are the same
    /// finding — startup re-reads its configuration many times, and an operator wants a
    /// list of what is broken, not a list of how often it was noticed.
    /// </summary>
    public string Identity => $"{Subsystem}|{Project}|{Trigger}|{Field}";

    public bool IsBlocking => Severity == StartupFindingSeverity.Blocking;
}
