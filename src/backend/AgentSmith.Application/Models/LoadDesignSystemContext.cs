using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-10-01-283dg: context for reading each repository's root DESIGN.md out of the
/// run's sandboxes into the pipeline, after the coding principles.
/// </summary>
public sealed record LoadDesignSystemContext(PipelineContext Pipeline) : ICommandContext;
