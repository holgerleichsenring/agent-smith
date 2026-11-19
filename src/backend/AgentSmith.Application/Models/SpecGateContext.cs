using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0315d: context for the SpecGate step — the spec ticket whose body
/// carries the fenced yaml spec (p0315c contract) plus the pipeline bag the
/// validated spec and its execution plan are published into.
/// <para>2026-11-19-6b0ba: <paramref name="SpecId"/> is set on the gate a spec derivation splices after the master
/// that wrote that spec; null on the front gate.</para>
/// </summary>
public sealed record SpecGateContext(
    Ticket Ticket,
    PipelineContext Pipeline,
    string? SpecId = null) : ICommandContext;
