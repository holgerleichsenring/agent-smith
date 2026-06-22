using System.ComponentModel;
using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0177/p0280: <c>read_sub_agent_observations</c> tool. Returns a child's final answer
/// (its full findings) from the in-memory <see cref="IChildAnswerStore"/>, so the master
/// can pull the detail behind a spawn_agents anchor count and synthesise it. p0280
/// repointed this from the never-implemented Redis IRunEventReader to the functional
/// in-memory store; the dashboard's per-child event timeline is a separate concern.
/// </summary>
public sealed class ReadSubAgentObservationsToolHost : IToolHost
{
    private readonly IChildAnswerStore _store;

    public ReadSubAgentObservationsToolHost(IChildAnswerStore store) => _store = store;

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = phase;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(ReadObservations, name: "read_sub_agent_observations")];
    }

    [Description(
        "Read one child's full findings (its final answer) behind a spawn_agents anchor " +
        "count. Pass the sub_agent_id returned by spawn_agents.")]
    public string ReadObservations(
        [Description("Sub-agent id from a prior spawn_agents result.")]
        string sub_agent_id)
    {
        if (string.IsNullOrWhiteSpace(sub_agent_id))
            return JsonSerializer.Serialize(new { error = "sub_agent_id required" });
        if (_store.TryGet(sub_agent_id, out var answer))
            return JsonSerializer.Serialize(new { sub_agent_id, answer });
        return JsonSerializer.Serialize(new { sub_agent_id, answer = (string?)null, note = "no answer recorded for this sub-agent" });
    }
}
