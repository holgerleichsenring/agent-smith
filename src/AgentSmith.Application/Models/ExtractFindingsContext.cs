using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

public sealed record ExtractFindingsContext(
    PipelineContext Pipeline) : ICommandContext;
