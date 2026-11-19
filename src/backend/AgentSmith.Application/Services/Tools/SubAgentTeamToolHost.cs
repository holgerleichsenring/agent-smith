using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-11-18-c8e2a: what a master does with the children it started, beside spawn_agents — write to one while it
/// works, continue one that finished with feedback, and wait for the next to finish. A child's answer reaches the
/// master as a notice with its next tool result, so waiting returns only who still runs.
/// </summary>
public sealed class SubAgentTeamToolHost(
    SubAgentTeam team, ISubAgentRunner runner, SubAgentContext context, IDecisionLogger decisionLogger, SubAgentEvents events)
    : IToolHost
{
    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? executionStage, string? investigatorMode) =>
    [
        AIFunctionFactory.Create(MessageAsync, name: "send_message", description:
            "Writes to a sub-agent you started, by its id or name: a running one reads it with its next tool result; a "
            + "finished one continues with it as its next turn, keeping everything it did so far, and its answer "
            + "reaches you when it finishes again. wait: true (with or without a message) then waits until one of your "
            + "sub-agents finishes — its answer comes with this result — or none is running."),
    ];

    /// <summary>2026-11-18-9c1cd: send_message and wait_for_agents as one tool.</summary>
    public async Task<string> MessageAsync(
        [Description("The sub-agent's id, as spawn_agents returned it, or its name. Omit to only wait.")] string? agent_id = null,
        [Description("What you tell it: feedback, a correction, the next thing to do.")] string? text = null,
        [Description("True waits until a sub-agent finishes, or none is running.")] bool wait = false,
        CancellationToken ct = default)
    {
        var sent = string.IsNullOrWhiteSpace(agent_id) && string.IsNullOrWhiteSpace(text) ? null
            : string.IsNullOrWhiteSpace(agent_id) || string.IsNullOrWhiteSpace(text)
                ? "Error: a message needs both agent_id and text; to only wait, pass wait: true and neither."
                : await SendMessage(agent_id, text, ct);
        if (sent is null && !wait) return "Error: pass agent_id and text to write to a sub-agent, or wait: true to wait for one.";
        if (!wait || sent?.StartsWith("Error", StringComparison.Ordinal) == true) return sent!;
        var waited = await WaitForAgents(ct);
        return sent is null ? waited : $"{sent}\n{waited}";
    }

    public async Task<string> SendMessage(
        string agent_id,
        string text,
        CancellationToken ct = default)
    {
        if (team.Find(agent_id) is not { } member)
            return $"Error: no sub-agent has the id '{agent_id}'. Yours: {Roster()}.";
        if (member.TryPost(text))
        {
            await events.MessagedAsync(member.Id, context, text, continued: false, ct); // 2026-11-18-c8e2d
            return $"Delivered: {member.Name} reads it with its next tool result.";
        }
        await events.MessagedAsync(member.Id, context, text, continued: true, ct);
        runner.Continue(member, text, context, (m, result) => decisionLogger.LogAsync(
            repositoryFiles: null, DecisionCategory.Implementation,
            $"Continued sub-agent {m.Name} ({m.Spec.Activity}) — {result.Status} cost ${result.CostUsd:F4}", CancellationToken.None), ct);
        return $"{member.Name} continues with your message; its answer reaches you when it finishes.";
    }

    public async Task<string> WaitForAgents(CancellationToken ct = default)
    {
        await team.WaitAsync(ct);
        var running = team.Running;
        return running.Count == 0
            ? "No sub-agent is running."
            : $"Still running: {string.Join(", ", running.Select(m => $"{m.Name} ({m.Id})"))}.";
    }

    private string Roster() => team.Members.Count == 0
        ? "none"
        : string.Join(", ", team.Members.Select(m => $"{m.Name} ({m.Id}{(m.IsRunning ? ", running" : "")})"));
}
