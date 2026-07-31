using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Server.Contracts;

/// <summary>
/// p0391a: one startup dependency, asked whether it is there. A probe REPORTS — it
/// returns findings and never throws, so the subsystem behind it can be missing without
/// taking the process with it. The listener has already bound its port by the time
/// probes run, so whatever a probe finds can be told to the operator.
/// </summary>
public interface IStartupProbe
{
    string Subsystem { get; }

    Task<IReadOnlyList<StartupFinding>> ProbeAsync(CancellationToken cancellationToken);
}
