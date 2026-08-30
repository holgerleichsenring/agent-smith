using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-08-30-18e3: context for the step that checks the scan master's stated entry map.
/// Carries only the pipeline — the claims, the read set and the findings are all on it.
/// </summary>
public sealed record AccountEntryStationsContext(PipelineContext Pipeline) : ICommandContext;
