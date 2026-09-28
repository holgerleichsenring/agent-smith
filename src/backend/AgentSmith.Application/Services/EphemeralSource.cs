namespace AgentSmith.Application.Services;

/// <summary>
/// The path an agent-mode scan's synthetic local repository carries when the operator
/// gave no --source-path: a directory that never exists, so the run is passive.
/// </summary>
public static class EphemeralSource
{
    public const string NoSourcePath = "/var/empty/agentsmith-noop";
}
