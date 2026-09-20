using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-09-19-4c1f: context for the spec-dialog grounding step. The step reads the turn's
/// scope off the pipeline itself — the repository list and the sandbox map the turn seeded
/// — so there is nothing left for the builder to resolve.
/// </summary>
public sealed record GroundSpecDialogContext(PipelineContext Pipeline) : ICommandContext;
