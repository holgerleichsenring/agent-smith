using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Audit-sink host: exposes the LogDecision tool in every phase. The decision
/// log is the cross-cutting trace of agent choices and is never phase-gated.
/// </summary>
public sealed class LogDecisionToolHost : IToolHost
{
    private readonly IDecisionLogger _decisionLogger;
    private readonly string _repoPath;
    private readonly List<PlanDecision> _decisions = new();

    public LogDecisionToolHost(IDecisionLogger decisionLogger, string repoPath = "/work")
    {
        _decisionLogger = decisionLogger;
        _repoPath = repoPath;
    }

    public IReadOnlyList<PlanDecision> GetDecisions() => _decisions.AsReadOnly();

    public IEnumerable<AIFunction> GetTools(SkillExecutionPhase? phase, string? investigatorMode)
    {
        _ = phase;
        _ = investigatorMode;
        return [AIFunctionFactory.Create(LogDecision, name: "log_decision")];
    }

    [Description("Logs a key architectural, tooling, implementation, or trade-off decision.")]
    public async Task<string> LogDecision(
        [Description("Category: Architecture, Tooling, Implementation, or TradeOff.")] string category,
        [Description("One-line description of the decision and its rationale.")] string decision,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<DecisionCategory>(category, ignoreCase: true, out var cat))
            return $"Error: invalid category '{category}'.";
        await _decisionLogger.LogAsync(_repoPath, cat, decision, ct);
        _decisions.Add(new PlanDecision(category, decision));
        return $"Decision logged: [{category}] {decision}";
    }
}
