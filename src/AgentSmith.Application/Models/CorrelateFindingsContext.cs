using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

public sealed record CorrelateFindingsContext(PipelineContext Pipeline) : ICommandContext;
