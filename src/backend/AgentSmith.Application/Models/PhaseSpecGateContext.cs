using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0315d: context for the PhaseSpecGate step — the phase ticket whose body
/// carries the fenced yaml spec (p0315c contract) plus the pipeline bag the
/// validated spec and its execution plan are published into.
/// </summary>
public sealed record PhaseSpecGateContext(
    Ticket Ticket,
    PipelineContext Pipeline) : ICommandContext;
