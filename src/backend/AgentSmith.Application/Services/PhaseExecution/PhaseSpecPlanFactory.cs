using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.PhaseExecution;

/// <summary>
/// p0315d: turns a validated phase spec into the approved <see cref="Plan"/>
/// the coding-agent-master executes — the spec's ordered steps ARE the plan
/// (the operator confirmed them in-thread before p0315c filed the ticket), so
/// the master's existing "Approved plan — execute this" contract drives
/// step-by-step spec-first execution without any skill change.
/// </summary>
public sealed class PhaseSpecPlanFactory
{
    public Plan Build(PhaseDraft draft)
    {
        var map = OutcomeYamlReader.ReadMap(draft.Yaml);
        var steps = (OutcomeYamlReader.GetList(map, "steps") ?? [])
            .Select((step, index) => new PlanStep(index + 1, DescribeStep(step), null, "implement"))
            .Where(s => !string.IsNullOrWhiteSpace(s.Description))
            .ToList();
        return new Plan(draft.Goal, steps, draft.Yaml);
    }

    private static string DescribeStep(object? step)
    {
        if (step is not Dictionary<object, object?> map) return step?.ToString() ?? string.Empty;
        var action = map.TryGetValue("action", out var a) ? a as string : null;
        var id = map.TryGetValue("id", out var i) ? i as string : null;
        return id is not null && action is not null ? $"{id}: {action}" : action ?? id ?? string.Empty;
    }
}
