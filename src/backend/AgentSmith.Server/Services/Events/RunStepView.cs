namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0388b: one row of the run's execution rail, served from the RunStep
/// projection with the per-step aggregates joined from the p0388a-attributed
/// child rows. One row per step, so the rail's payload is O(steps) — flat over
/// the run's lifetime — instead of the O(runtime) event log the dashboard used
/// to fold client-side.
/// <para>
/// p0395: a spliced phase step (p0393a) is projected with the phase id composed
/// into its names ("p19106a: Generate plan"). The read path splits that back
/// apart: <see cref="PhaseId"/> carries the phase, and StepName/DisplayName are
/// served clean — for old prefixed rows and new ones alike — so the dashboard
/// can group by phase instead of truncating every row on the same prefix.
/// </para>
/// </summary>
public sealed record RunStepView(
    int StepIndex,
    string StepName,
    string? DisplayName,
    string? CommandName,
    string Status,
    double? DurationSeconds,
    string? ResultMessage,
    int LlmCalls,
    decimal CostUsd,
    int SandboxCommands,
    int SubAgents,
    string? PhaseId = null);
