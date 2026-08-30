using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-08-30-3c12: context for the step that settles the requirement answers the scan
/// stated. Carries only the pipeline — the answers, the entry map, the read set and the
/// findings all live on it.
/// </summary>
public sealed record AccountRequirementAnswersContext(PipelineContext Pipeline) : ICommandContext;
