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
        return [AIFunctionFactory.Create(SpawnAgents, name: "spawn_agents")];
    }

    [Description(
        "Spawn one or more sub-agents in parallel. Each task carries a non-generic " +
        "name (descriptive role, not 'agent1' or 'worker'), a one-line activity, the " +
        "task description, and the inherited context block (pipeline goal + prior " +
        "context slice). Returns decision-anchor counts per child — pull observation " +
        "detail lazily via read_sub_agent_observations.")]
    public async Task<string> SpawnAgents(
        [Description("Array of task objects: name, activity, task_description, " +
                     "optional output_hint, optional tool_profile, inherited_context " +
                     "object with pipeline_goal + prior_context_slice + " +
                     "optional system_prompt_block.")]
        JsonElement tasks,
        CancellationToken ct = default)
    {
        var specs = ParseTasks(tasks, out var rejections);
        if (specs.Count == 0)
            return JsonSerializer.Serialize(new { results = rejections, granted = 0 });

        var granted = _budget.TryReserve(specs.Count);
        var (toRun, deferred) = SplitByBudget(specs, granted);

        var runResults = toRun.Count == 0
            ? Array.Empty<SubAgentResult>()
            : (IReadOnlyList<SubAgentResult>)await _runner.RunAsync(toRun, _context, ct);

        await LogDecisionAsync(toRun, runResults, ct);

        var combined = new List<object>(rejections.Cast<object>());
        for (var i = 0; i < runResults.Count; i++)
        {
            var r = runResults[i];
            combined.Add(new
            {
                task_index = r.TaskIndex,
                status = r.Status.ToString(),
                sub_agent_id = r.SubAgentId,
                name = r.Name,
                observations_count = r.ObservationsCount,
                findings_count = r.FindingsCount,
                files_written_count = r.FilesWrittenCount,
                tool_calls = r.ToolCalls,
                cost_usd = r.CostUsd,
            });
        }
        for (var i = 0; i < deferred.Count; i++)
        {
            combined.Add(new
            {
                task_index = granted + i,
                status = SubAgentStatus.Failed.ToString(),
                sub_agent_id = (string?)null,
                name = deferred[i].Name,
                observations_count = 0,
                findings_count = 0,
                files_written_count = 0,
                tool_calls = 0,
                cost_usd = 0m,
                reason = "budget_exhausted",
            });
        }
        return JsonSerializer.Serialize(new { results = combined, granted });
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
            var toolProfile = task.TryGetProperty("tool_profile", out var tp)
                && Enum.TryParse<ToolProfile>(tp.GetString(), ignoreCase: true, out var parsed)
                    ? parsed : ToolProfile.Investigator;

            var inherited = ParseInheritedContext(task);
            specs.Add(new SubAgentSpec(name, activity, taskDescription, inherited, outputHint, toolProfile));
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
            "/work", DecisionCategory.Implementation,
            $"Spawned {ranSpecs.Count} sub-agents: {summary}", ct);
    }
}
