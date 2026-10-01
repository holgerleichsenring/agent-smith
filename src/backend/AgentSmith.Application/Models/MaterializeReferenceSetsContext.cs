using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-10-01-283df: carry the uploaded websites the approval cites into the run. The handler reads
/// the published approval and the sandboxes straight from the pipeline, so the context only carries it.
/// </summary>
public sealed record MaterializeReferenceSetsContext(PipelineContext Pipeline) : ICommandContext;
