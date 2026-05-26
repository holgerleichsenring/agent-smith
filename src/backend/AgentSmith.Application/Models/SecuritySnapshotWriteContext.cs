using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for writing a security scan snapshot to .agentsmith/security/.
/// Called after CompileFindings to persist the current scan's snapshot for trend analysis.
/// </summary>
public sealed record SecuritySnapshotWriteContext(
    PipelineContext Pipeline) : ICommandContext;
