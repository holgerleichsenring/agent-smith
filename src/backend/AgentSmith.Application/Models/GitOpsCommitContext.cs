using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-23-c81eb: context for the GitOpsCommit step — the poc- project (its one repo is the target's source
/// repo, which holds the app template and the deploy folders) and the run's seeds.</summary>
public sealed record GitOpsCommitContext(PipelineContext Pipeline, ResolvedProject Project, PocSeeds Seeds) : ICommandContext;
