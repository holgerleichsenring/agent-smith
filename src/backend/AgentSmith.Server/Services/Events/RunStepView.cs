namespace AgentSmith.Server.Services.Events;

/// <summary>
/// p0388b: one row of the run's execution rail, served from the RunStep
/// projection with the per-step aggregates joined from the p0388a-attributed
/// child rows. One row per step, so the rail's payload is O(steps) — flat over
/// the run's lifetime — instead of the O(runtime) event log the dashboard used
/// to fold client-side.
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
    int SubAgents);
