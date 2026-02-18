using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Commands.Contexts;

/// <summary>
/// Context for running tests against code changes.
/// </summary>
public sealed record TestContext(
    Repository Repository,
    IReadOnlyList<CodeChange> Changes,
    PipelineContext Pipeline) : ICommandContext;
