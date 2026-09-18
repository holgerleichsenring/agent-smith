using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0393a: input for the DeriveSpec step. The repo list is the RESOLVED scope in
/// order — the spec set lives in exactly ONE repo, the first of them, and the
/// database pointer records which so a later run with a different scope still
/// finds it.
/// </summary>
public sealed record DeriveSpecContext(
    Ticket? Ticket,
    TrackerConnection? Tracker,
    IReadOnlyList<RepoConnection> Repos,
    AgentConfig AgentConfig,
    PipelineContext Pipeline) : ICommandContext;

/// <summary>p0393a: input for the hand-back step.</summary>
public sealed record SpecHandbackContext(
    Ticket? Ticket,
    TrackerConnection? Tracker,
    IReadOnlyList<RepoConnection> Repos,
    PipelineContext Pipeline) : ICommandContext;

/// <summary>p0393a: input for the phase-sequence splice and the per-phase selection.</summary>
public sealed record PhaseSequenceContext(PipelineContext Pipeline) : ICommandContext;

/// <summary>p0393a: input for SelectPhase — the phase id travels on the command.</summary>
public sealed record SelectPhaseContext(string PhaseId, PipelineContext Pipeline) : ICommandContext;

/// <summary>2026-09-17-0e79c: input for the premise check — the agent that asks, and the
/// tracker the hand-back comments on. The phase itself is the CURRENT one, published by
/// SelectPhase, because the check is about the phase the run is entering.</summary>
public sealed record CheckPhasePremisesContext(
    AgentConfig AgentConfig,
    TrackerConnection? Tracker,
    PipelineContext Pipeline) : ICommandContext;
