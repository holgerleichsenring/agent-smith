using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;

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
/// <para>
/// p0398: <see cref="StepClass"/> is the command's static display class
/// (milestone / gate / internal, from CommandStepClasses) and
/// <see cref="HasFinding"/> says whether a gate has something to say (not-ok,
/// or a summary that is not one of its known no-op sentences). The read path
/// decides in one place; the drawer only renders.
/// </para>
/// <para>
/// p0404: <see cref="Time"/> is where the step's wall-clock went — model,
/// throttle (a subset of model), sandbox, and the scaffolding remainder. Read
/// with <see cref="SandboxCommands"/> it also answers serialisation: N commands
/// whose summed duration approaches the step's own ran one after another.
/// </para>
/// <para>
/// p0405: the rail is ONE ordered sequence — the steps that ran, then the ones
/// the executor has announced but not reached. A row with <see cref="Planned"/>
/// carries its index, its phase and its display name and NOTHING else: no
/// status, no cost, no duration, because an unreached step has none. The nulls
/// are the contract, not an omission — a skeleton borrowing the vocabulary of a
/// finished step would invite the same confusion in the other direction.
/// </para>
/// </summary>
public sealed record RunStepView(
    int StepIndex,
    string StepName,
    string? DisplayName,
    string? CommandName,
    string? Status,
    double? DurationSeconds,
    string? ResultMessage,
    int? LlmCalls,
    decimal? CostUsd,
    int? SandboxCommands,
    int? SubAgents,
    string? PhaseId = null,
    string StepClass = CommandStepClasses.Milestone,
    bool HasFinding = false,
    RunTimeSplitView? Time = null,
    bool Planned = false)
{
    /// <summary>
    /// p0405: the announced-but-unreached row. Everything a step only HAS once it
    /// runs is left null by construction — the caller cannot forget one.
    /// </summary>
    public static RunStepView ForPlanned(PlannedStepView planned) =>
        new(planned.StepIndex, planned.DisplayName, planned.DisplayName, planned.CommandName,
            Status: null, DurationSeconds: null, ResultMessage: null,
            LlmCalls: null, CostUsd: null, SandboxCommands: null, SubAgents: null,
            planned.PhaseId, CommandStepClasses.Get(planned.CommandName), HasFinding: false,
            Time: null, Planned: true);
}
