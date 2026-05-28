using System.ComponentModel;
using System.Text.Json;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Events;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// p0177: <c>read_sub_agent_observations</c> tool. Filters the per-run
/// event stream by sub-agent id + optional kinds + max_results, returning
/// the matched events page-by-page so the master can pull detail behind
/// a decision-anchor count <i>without</i> turning the child's trace into
/// a distilled summary. Bus is the single source of truth; this host is
/// the lens, not the compressor.
///
/// <para>Available to both master and children — siblings may inspect
/// each other's observations. The topology bound is spawn, not read.</para>
/// </summary>
public sealed class ReadSubAgentObservationsToolHost : IToolHost
{
    private readonly IRunEventReader _reader;
    private readonly string _runId;

    public ReadSubAgentObservationsToolHost(IRunEventReader reader, string runId)
    {
        _reader = reader;
        _runId = runId;
    }

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = phase;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(ReadObservations, name: "read_sub_agent_observations")];
    }

    [Description(
        "Read the typed sub-agent event stream for one child. Returns paged " +
        "typed events (no summarisation) filtered by sub_agent_id and the " +
        "optional kinds list. Use this when you need detail behind an anchor " +
        "count returned by spawn_agents.")]
    public async Task<string> ReadObservations(
        [Description("Sub-agent id from a prior spawn_agents result.")]
        string sub_agent_id,
        [Description("Optional kinds filter. Values: observation, finding, " +
                     "file_written, tool_call, spawned, completed. Defaults to all.")]
        string[]? kinds = null,
        [Description("Max results to return; default 50, max 500.")]
        int max_results = 50,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sub_agent_id))
            return JsonSerializer.Serialize(new { error = "sub_agent_id required", results = Array.Empty<object>() });

        var cap = Math.Clamp(max_results, 1, 500);
        var allEvents = await _reader.ReadAsync(_runId, ct);
        var allowed = MapKindsToTypes(kinds);

        var matched = allEvents
            .Where(e => MatchesSubAgent(e, sub_agent_id))
            .Where(e => allowed is null || allowed.Contains(e.Type))
            .Take(cap)
            .Select(ProjectEvent)
            .ToList();

        return JsonSerializer.Serialize(new { results = matched, count = matched.Count });
    }

    private static HashSet<EventType>? MapKindsToTypes(string[]? kinds)
    {
        if (kinds is null || kinds.Length == 0) return null;
        var set = new HashSet<EventType>();
        foreach (var kind in kinds)
        {
            switch (kind?.ToLowerInvariant())
            {
                case "observation": set.Add(EventType.SubAgentObservation); break;
                case "finding": set.Add(EventType.SubAgentFinding); break;
                case "file_written": set.Add(EventType.SubAgentFileWritten); break;
                case "tool_call": set.Add(EventType.SubAgentToolCall); break;
                case "spawned": set.Add(EventType.SubAgentSpawned); break;
                case "completed": set.Add(EventType.SubAgentCompleted); break;
            }
        }
        return set;
    }

    private static bool MatchesSubAgent(RunEvent evt, string subAgentId) => evt switch
    {
        SubAgentSpawnedEvent e => e.SubAgentId == subAgentId,
        SubAgentObservationEvent e => e.SubAgentId == subAgentId,
        SubAgentFindingEvent e => e.SubAgentId == subAgentId,
        SubAgentFileWrittenEvent e => e.SubAgentId == subAgentId,
        SubAgentToolCallEvent e => e.SubAgentId == subAgentId,
        SubAgentCompletedEvent e => e.SubAgentId == subAgentId,
        _ => false,
    };

    private static object ProjectEvent(RunEvent evt) => evt switch
    {
        SubAgentSpawnedEvent e => new { kind = "spawned", e.SubAgentId, e.Name, e.Activity, e.ParentSubAgentId, e.Timestamp },
        SubAgentObservationEvent e => new { kind = "observation", e.SubAgentId, e.Text, e.Timestamp },
        SubAgentFindingEvent e => new { kind = "finding", e.SubAgentId, e.Severity, e.Title, e.Detail, e.Timestamp },
        SubAgentFileWrittenEvent e => new { kind = "file_written", e.SubAgentId, e.Path, e.Bytes, e.Timestamp },
        SubAgentToolCallEvent e => new { kind = "tool_call", e.SubAgentId, e.ToolName, e.ArgsSummary, e.Timestamp },
        SubAgentCompletedEvent e => new
        {
            kind = "completed",
            e.SubAgentId,
            e.Status,
            e.ObservationsCount,
            e.FindingsCount,
            e.FilesWrittenCount,
            e.ToolCalls,
            e.CostUsd,
            e.Timestamp,
        },
        _ => new { kind = "unknown", evt.Type, evt.Timestamp },
    };
}
