using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0428: context for the preflight gate. Every check reads what it needs from the pipeline
/// itself, so the step carries no payload of its own.
/// </summary>
/// <param name="NeedsSandbox">2026-11-16-f3fba: which half runs — true for RunPreflight (the checks that
/// read a sandbox), false for RunConfigPreflight (the checks config and the host decide).</param>
public sealed record RunPreflightContext(PipelineContext Pipeline, bool NeedsSandbox = true) : ICommandContext;
