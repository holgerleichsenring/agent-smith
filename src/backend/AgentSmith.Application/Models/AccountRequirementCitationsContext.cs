using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-08-30-03e1: context for the step that settles what each station examined and what
/// the scan cited for it. The pipeline is all it needs — the entry map, the read set and
/// the citations all travel on it.
/// </summary>
public sealed record AccountRequirementCitationsContext(PipelineContext Pipeline) : ICommandContext;
