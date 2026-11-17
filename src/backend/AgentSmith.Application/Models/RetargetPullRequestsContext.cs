using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-17-9a39c: context for the RetargetPullRequests step — the run's project, whose repos the moves name.</summary>
public sealed record RetargetPullRequestsContext(PipelineContext Pipeline, ResolvedProject Project) : ICommandContext;
