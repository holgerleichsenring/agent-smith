using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0428: context for the RunPreflight step. Every check reads what it needs from the
/// pipeline itself, so the step carries no payload of its own.
/// </summary>
public sealed record RunPreflightContext(PipelineContext Pipeline) : ICommandContext;
