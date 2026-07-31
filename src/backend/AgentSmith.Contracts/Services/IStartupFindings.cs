using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// p0391a: the live list of what is wrong with this installation. Startup dependencies
/// and configuration rules record into it instead of throwing; the findings endpoint and
/// the dashboard read it. <see cref="Clear"/> lets a subsystem re-probe and publish a
/// current picture, so a fault that heals stops being reported.
/// </summary>
public interface IStartupFindings
{
    void Record(StartupFinding finding);

    void Clear(string subsystem);

    IReadOnlyList<StartupFinding> All { get; }
}
