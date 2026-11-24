using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Application.Models;

/// <summary>2026-11-23-933dc: context for the PocSourceCommit step — the poc- project (its one repo is the target's
/// source repo) and the run's seeds.</summary>
public sealed record PocSourceCommitContext(PipelineContext Pipeline, ResolvedProject Project, PocSeeds Seeds) : ICommandContext;
