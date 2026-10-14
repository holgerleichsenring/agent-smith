using AgentSmith.Infrastructure.Persistence.Entities;

namespace AgentSmith.Server.Models;

/// <summary>
/// 2026-10-13-9a59n: a run's last launch — its newest segment — and the run's sandboxes that ended vanished
/// or OOM-killed, which <see cref="Services.Tickets.InfrastructureEnd"/> places inside it by position.
/// </summary>
public sealed record RunLastLaunch(RunSegment Segment, IReadOnlyList<RunSandbox> SandboxEnds);
