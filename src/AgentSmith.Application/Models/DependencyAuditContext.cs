using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Models;

public sealed record DependencyAuditContext(
    PipelineContext Pipeline) : ICommandContext;
