namespace AgentSmith.Server.Services.Sandbox;

/// <summary>How a one-shot container ended: its exit code, and its output when it failed.</summary>
public sealed record DockerOneShotExit(long StatusCode, string Logs)
{
    public bool Succeeded => StatusCode == 0;

    public string LogsOrEmpty => string.IsNullOrWhiteSpace(Logs) ? "<empty>" : Logs.Trim();
}
