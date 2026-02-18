using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Configuration;
using AgentSmith.Domain.ValueObjects;

namespace AgentSmith.Application.Commands.Contexts;

/// <summary>
/// Context for checking out a source repository.
/// </summary>
public sealed record CheckoutSourceContext(
    SourceConfig Config,
    BranchName Branch,
    PipelineContext Pipeline) : ICommandContext;
