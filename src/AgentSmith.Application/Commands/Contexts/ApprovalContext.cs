using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Commands.Contexts;

/// <summary>
/// Context for requesting user approval of an execution plan.
/// </summary>
public sealed record ApprovalContext(
    Plan Plan,
    PipelineContext Pipeline) : ICommandContext;
