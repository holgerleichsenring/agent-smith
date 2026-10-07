using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0315d: context for the WritePhaseRecord step — the checked-out repository plus the
/// pipeline bag carrying the spec.
/// </summary>
/// <param name="Repos">2026-10-06-03c7e: the run's repositories, or the project's when the run
/// scoped none — what the carrying repository is resolved against.</param>
public sealed record WritePhaseRecordContext(
    Repository Repository,
    PipelineContext Pipeline,
    IReadOnlyList<RepoConnection> Repos) : ICommandContext;
