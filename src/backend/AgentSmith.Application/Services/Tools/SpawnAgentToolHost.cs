using System.ComponentModel;
using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0177: exposes the <c>spawn_agents</c> tool that lets the master agent
/// fan out into n typed sub-agents. The handler validates names without an
/// LLM call (the validator is the structural reject path), reserves budget,
/// dispatches to <see cref="ISubAgentRunner"/>, logs the decision, and
/// returns the decision-anchor list to the master. Result rows carry
/// counts, IDs, and cost — never distilled text.
/// </summary>
public sealed class SpawnAgentToolHost : IToolHost
{
    private readonly ISubAgentRunner _runner;
    private readonly SubAgentBudget _budget;
    private readonly SubAgentNameValidator _nameValidator;
    private readonly IDecisionLogger _decisionLogger;
    private readonly SubAgentContext _context;

    public SpawnAgentToolHost(
        ISubAgentRunner runner,
        SubAgentBudget budget,
        SubAgentNameValidator nameValidator,
        IDecisionLogger decisionLogger,
        SubAgentContext context)
    {
        _runner = runner;
        _budget = budget;
        _nameValidator = nameValidator;
        _decisionLogger = decisionLogger;
        _context = context;
    }

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = phase;
        _ = investigatorMode;
        // 2026-11-11-205fj: a design turn's context carries its child ceiling; its budget is the turn's own count.
        var description = _context.ChildIterationCeiling is { } rounds
            ? SpawnAgentsDescription.Design(_budget.MaxPerRun, rounds)
            : SpawnAgentsDescription.Run;
        return [AIFunctionFactory.Create(SpawnAgents, name: "spawn_agents", description: description)];
    }

    public async Task<string> SpawnAgents(
        [Description("Array of task objects: name, activity, task_description, " +
                     "optional output_hint, inherited_context " +
                     "object with pipeline_goal + prior_context_slice + " +
                     "optional system_prompt_block.")]
        JsonElement tasks,
        [Description("True starts the children and returns their ids at once; false (the default) waits for their answers.")]
        bool background = false,
        CancellationToken ct = default)
    {
        var specs = ParseTasks(tasks, out var rejections);
        if (specs.Count == 0)
            return JsonSerializer.Serialize(new { results = rejections, granted = 0 });

        var granted = _budget.TryReserve(specs.Count);
        var (toRun, deferred) = SplitByBudget(specs, granted);
        if (background && _context.Team is not null) return Started(toRun, rejections, deferred, granted, ct);

        var runResults = toRun.Count == 0
            ? Array.Empty<SubAgentResult>()
            : (IReadOnlyList<SubAgentResult>)await _runner.RunAsync(toRun, _context, ct);

        await LogDecisionAsync(toRun, runResults, ct);

        var combined = rejections.Concat(runResults.Select(SpawnResultRows.Answered))
            .Concat(SpawnResultRows.Refused(deferred, granted, _budget.Exhausted)).ToList();
        var cut = runResults.Any(r => r.Answer?.StartsWith(SubAgentAnswer.CallCutPrefix, StringComparison.Ordinal) == true); // 85e2
        return JsonSerializer.Serialize(cut ? new { cut = SubAgentAnswer.CallCutLine, results = combined, granted } : (object)new { results = combined, granted });
    }

    // 2026-11-18-c8e2a: the children run in the background; each one's decision line is logged when it finishes.
    private string Started(
        IReadOnlyList<SubAgentSpec> toRun, List<object> rejections, IReadOnlyList<SubAgentSpec> deferred, int granted,
        CancellationToken ct)
    {
        var members = toRun.Count == 0 ? [] : _runner.Start(toRun, _context,
            (member, result) => LogDecisionAsync([member.Spec], [result], CancellationToken.None), ct);
        var rows = rejections.Concat(members.Select(SpawnResultRows.Started)).Concat(SpawnResultRows.Refused(deferred, granted, _budget.Exhausted));
        return JsonSerializer.Serialize(new { results = rows.ToList(), granted });
    }

    private List<SubAgentSpec> ParseTasks(JsonElement tasks, out List<object> rejections)
    {
        rejections = new();
        var specs = new List<SubAgentSpec>();
        if (tasks.ValueKind != JsonValueKind.Array) return specs;

        foreach (var task in tasks.EnumerateArray())
        {
            var name = task.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
            var rejectReason = _nameValidator.Reject(name);
            if (rejectReason is not null)
            {
                rejections.Add(new
                {
                    status = SubAgentStatus.Failed.ToString(),
                    name,
                    reason = "invalid_name",
                    detail = rejectReason,
                });
                continue;
            }

            var activity = task.TryGetProperty("activity", out var a) ? a.GetString() ?? "" : "";
            var taskDescription = task.TryGetProperty("task_description", out var d) ? d.GetString() ?? "" : "";
            var outputHint = task.TryGetProperty("output_hint", out var o) ? o.GetString() : null;

            var inherited = ParseInheritedContext(task);
            specs.Add(new SubAgentSpec(name, activity, taskDescription, inherited, outputHint));
        }
        return specs;
    }

    private static InheritedContext ParseInheritedContext(JsonElement task)
    {
        if (!task.TryGetProperty("inherited_context", out var ic) || ic.ValueKind != JsonValueKind.Object)
            return new InheritedContext("", "");
        var goal = ic.TryGetProperty("pipeline_goal", out var g) ? g.GetString() ?? "" : "";
        var slice = ic.TryGetProperty("prior_context_slice", out var p) ? p.GetString() ?? "" : "";
        var block = ic.TryGetProperty("system_prompt_block", out var s) ? s.GetString() : null;
        return new InheritedContext(goal, slice, block);
    }

    private static (List<SubAgentSpec> toRun, List<SubAgentSpec> deferred) SplitByBudget(
        IReadOnlyList<SubAgentSpec> specs, int granted)
    {
        var run = specs.Take(granted).ToList();
        var deferred = specs.Skip(granted).ToList();
        return (run, deferred);
    }

    private async Task LogDecisionAsync(
        IReadOnlyList<SubAgentSpec> ranSpecs, IReadOnlyList<SubAgentResult> results,
        CancellationToken ct)
    {
        if (ranSpecs.Count == 0) return;
        // p0341e: append the failure REASON for a failed child so the decision names the cause
        // (was a bare "Failed cost $0.0000" that hid WHY in the pod logs). Succeeded children
        // carry no reason, so the suffix is empty for them.
        var summary = string.Join(
            "; ",
            ranSpecs.Zip(results, (s, r) =>
                $"{s.Name} ({s.Activity}) — {r.Status} cost ${r.CostUsd:F4}"
                + (r.Status == SubAgentStatus.Failed && !string.IsNullOrWhiteSpace(r.FailureReason)
                    ? $" — {r.FailureReason}"
                    : string.Empty)));
        await _decisionLogger.LogAsync(
            repositoryFiles: null, DecisionCategory.Implementation,
            $"Spawned {ranSpecs.Count} sub-agents: {summary}", ct);
    }
}
