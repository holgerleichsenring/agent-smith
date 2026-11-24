using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-23-c81ec: context for the PocRolloutFollow step — the poc- project and the run's seeds.</summary>
public sealed record PocRolloutFollowContext(PipelineContext Pipeline, ResolvedProject Project, PocSeeds Seeds) : ICommandContext;
