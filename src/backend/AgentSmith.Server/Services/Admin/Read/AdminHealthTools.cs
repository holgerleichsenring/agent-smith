using System.ComponentModel;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Events;

namespace AgentSmith.Server.Services.Admin.Read;

/// <summary>
/// 2026-11-17-f3a8b: the server's health as this replica sees it — its startup and configuration findings and the
/// recent system events. The server keeps no log buffer; the events are what it holds.
/// </summary>
public sealed class AdminHealthTools(IStartupFindings findings, JobsBroadcaster? broadcaster = null)
{
    [Description("Reads this server replica's health: the startup and configuration findings (subsystem, severity, "
        + "reason, project, field) and the recent system events. Names the replica that answered.")]
    public string ReadHealth() => AdminToolJson.Render(new
    {
        Replica = Environment.MachineName,
        Findings = findings.All,
        RecentSystemEvents = broadcaster?.SystemRecent.Select(e => (object)e) ?? [],
        ServerLog = "not kept in memory — read the server's log where it runs",
    });
}
