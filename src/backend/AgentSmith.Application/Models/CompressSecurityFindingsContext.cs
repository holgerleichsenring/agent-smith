using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

public sealed record CompressSecurityFindingsContext(
    PipelineContext Pipeline) : ICommandContext;
