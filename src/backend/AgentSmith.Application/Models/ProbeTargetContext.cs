using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// 2026-09-01-379a: ask each context's declared target environment whether it answers,
/// after the prerequisites and before the master. The handler reads the sandboxes and the
/// per-sandbox context list straight from the pipeline, so the context only carries it.
/// </summary>
public sealed record ProbeTargetContext(PipelineContext Pipeline) : ICommandContext;
