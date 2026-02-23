using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// Context for committing .agentsmith/ files and creating a PR during project init.
/// Unlike CommitAndPRContext, no ticket is needed — the commit is a standalone init.
/// </summary>
public sealed record InitCommitContext(
    Repository Repository,
    SourceConfig SourceConfig,
    TicketConfig TicketConfig,
    PipelineContext Pipeline) : ICommandContext;
