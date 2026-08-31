namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-08-31-7097: a declared stage whose command shape names no binary that can be
/// probed — a shell invocation, an environment assignment, a subshell, a path or a
/// variable. It is carried, not dropped: a silent partial list is what makes a probe
/// look like a guarantee.
/// </summary>
public sealed record UnprobedStage(string ContextName, string StageLabel, string Command);
