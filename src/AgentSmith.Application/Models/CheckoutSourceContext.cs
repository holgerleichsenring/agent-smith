using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for checking out a source repository.
/// </summary>
public sealed record CheckoutSourceContext(
    SourceConfig Config,
    BranchName Branch,
    PipelineContext Pipeline) : ICommandContext;
