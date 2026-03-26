using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Models;

public sealed record GitHistoryScanContext(
    PipelineContext Pipeline) : ICommandContext;
