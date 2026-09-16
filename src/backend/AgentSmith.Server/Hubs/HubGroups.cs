namespace AgentSmith.Server.Hubs;

/// <summary>
/// SignalR group naming conventions for the JobsHub. The group-filter levels —
/// overview / run-specific / sandbox-specific / spec-dialog-specific — express the
/// subscription levels in the dashboard contract.
/// </summary>
public static class HubGroups
{
    public const string Overview = "overview";
    public static string Run(string runId) => $"run:{runId}";
    public static string Sandbox(string runId, string repo) => $"sandbox:{runId}:{repo}";
    /// <summary>
    /// p0173a: callers subscribe to this group to receive system-level
    /// events (poll cycles, webhook receipts, chat ingestion, config /
    /// catalog activity). One group for the whole dashboard — Source
    /// field on each event gives the client per-origin filtering.
    /// </summary>
    public const string System = "system";

    /// <summary>
    /// 2026-09-15-9033: one group per spec-dialog session, because a design conversation
    /// is addressed to the one person holding it rather than to the dashboard. Who may
    /// join is the hub method's question — the group name is only a name.
    /// </summary>
    public static string SpecDialog(string dialogId) => $"spec-dialog:{dialogId}";
}
