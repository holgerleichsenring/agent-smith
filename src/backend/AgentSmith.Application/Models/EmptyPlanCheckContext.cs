using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0140e: context for the empty-plan gate. The handler reads the Plan from the pipeline
/// context (set by GeneratePlanHandler) and the project / pipeline names for the
/// skipped-counter labels. No payload needed beyond the pipeline reference.
/// </summary>
public sealed record EmptyPlanCheckContext(PipelineContext Pipeline) : ICommandContext;
