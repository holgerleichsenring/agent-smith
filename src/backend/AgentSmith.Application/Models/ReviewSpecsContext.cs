using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>2026-10-13-8762g: input for the run-time spec review — the ticket whose text the reviewers are
/// shown, the run's repositories, and the agent the review calls are made under.</summary>
public sealed record ReviewSpecsContext(
    Ticket? Ticket,
    IReadOnlyList<RepoConnection> Repos,
    AgentConfig AgentConfig,
    PipelineContext Pipeline) : ICommandContext;
