using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Configuration;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Commands.Contexts;

/// <summary>
/// Context for committing changes and creating a pull request.
/// </summary>
public sealed record CommitAndPRContext(
    Repository Repository,
    IReadOnlyList<CodeChange> Changes,
    Ticket Ticket,
    SourceConfig SourceConfig,
    TicketConfig TicketConfig,
    PipelineContext Pipeline) : ICommandContext;
