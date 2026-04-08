using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for analyzing security scan trends by comparing current findings
/// with previously committed snapshots in .agentsmith/security/.
/// </summary>
public sealed record SecurityTrendContext(
    PipelineContext Pipeline) : ICommandContext;
