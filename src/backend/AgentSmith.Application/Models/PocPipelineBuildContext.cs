using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-23-933dd: context for the PocPipelineBuild step — the poc- project (its one repo is the target's
/// source repo, whose host runs the build) and the run's seeds.</summary>
public sealed record PocPipelineBuildContext(PipelineContext Pipeline, ResolvedProject Project, PocSeeds Seeds) : ICommandContext;
