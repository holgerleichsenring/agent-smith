using AgentSmith.Application.Services.Loop;

namespace AgentSmith.Application.Services.Tools;

/// <summary>p0177: the rows a spawn_agents result lists — an answered child, a started one (2026-11-18-c8e2a), and one
/// the budget refused.</summary>
internal static class SpawnResultRows
{
    internal static object Answered(SubAgentResult r) => new
    {
        task_index = r.TaskIndex,
        status = r.Status.ToString(),
        sub_agent_id = r.SubAgentId,
        name = r.Name,
        tool_calls = r.ToolCalls,
        cost_usd = r.CostUsd,
        answer = r.Answer, // 2026-11-09-0620e
    };

    internal static object Started(SubAgentMember m, int index) =>
        new { task_index = index, status = "Running", sub_agent_id = m.Id, name = m.Name };

    internal static IEnumerable<object> Refused(IReadOnlyList<SubAgentSpec> deferred, int granted, string exhausted) =>
        deferred.Select((d, i) => (object)new
        {
            task_index = granted + i,
            status = SubAgentStatus.Failed.ToString(),
            sub_agent_id = (string?)null,
            name = d.Name,
            observations_count = 0, findings_count = 0, files_written_count = 0,
            tool_calls = 0,
            cost_usd = 0m,
            reason = "budget_exhausted",
            detail = exhausted, // 2026-11-17-274ca
        });
}
