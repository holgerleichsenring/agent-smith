namespace AgentSmith.Server.Hubs;

/// <summary>
/// SignalR group naming conventions for the JobsHub. Three group-filter
/// levels — overview / run-specific / sandbox-specific — express the
/// subscription levels in the dashboard contract.
/// </summary>
public static class HubGroups
{
    public const string Overview = "overview";
    public static string Run(string runId) => $"run:{runId}";
    public static string Sandbox(string runId, string repo) => $"sandbox:{runId}:{repo}";
}
