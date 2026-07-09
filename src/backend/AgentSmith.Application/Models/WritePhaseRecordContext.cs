using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0315d: context for the WritePhaseRecord step — the checked-out repository
/// whose working tree receives the executed phase spec under
/// <c>.agentsmith/phases/done/</c>, plus the pipeline bag carrying the spec.
/// </summary>
public sealed record WritePhaseRecordContext(
    Repository Repository,
    PipelineContext Pipeline) : ICommandContext;
