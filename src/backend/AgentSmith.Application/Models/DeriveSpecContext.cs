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

/// <summary>p0393a: input for the spec-sequence splice and the per-spec selection.</summary>
public sealed record SpecSequenceContext(PipelineContext Pipeline) : ICommandContext;

/// <summary>p0393a: input for SelectSpec — the spec id travels on the command.</summary>
public sealed record SelectSpecContext(string SpecId, PipelineContext Pipeline) : ICommandContext;

/// <summary>2026-09-17-0e79c: input for the premise check — the agent that asks, and the
/// tracker the hand-back comments on. The spec itself is the CURRENT one, published by
/// SelectSpec, because the check is about the spec the run is entering.</summary>
public sealed record CheckSpecPremisesContext(
    AgentConfig AgentConfig,
    TrackerConnection? Tracker,
    PipelineContext Pipeline) : ICommandContext;
