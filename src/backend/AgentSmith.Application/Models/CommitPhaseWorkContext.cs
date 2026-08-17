using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Models;

/// <summary>
/// p0437: context for the step that puts a phase's work on the branch before the gate
/// that judges it. Carries only the pipeline — the checkpointer reads the repositories,
/// the sandboxes and the branch from it, exactly as it does on its opportunistic path.
/// </summary>
public sealed record CommitPhaseWorkContext(PipelineContext Pipeline) : ICommandContext;
