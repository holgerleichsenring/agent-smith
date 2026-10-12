using System.ComponentModel;
using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Repositories;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Domain.Entities;
using Microsoft.Extensions.AI;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// Audit-sink host: exposes the LogDecision tool in every phase. The decision
/// log is the cross-cutting trace of agent choices and is never filtered by execution phase.
/// <para>
/// 2026-09-19-c511a: the repository is a FILE SURFACE, and there is no default for it. The parameter
/// used to default to the sandbox mount "/work" as a host path, which is how every call site came to
/// pass a directory the server process cannot write; a default here is also how the next call site
/// would silently write nothing.
/// </para>
/// <para>
/// 2026-10-12-f5f7c: ONE host per run with a REPOSITORY map. The run-wide decision list stays
/// whole; only the file copy is routed — into the repository the call names, or the run's only
/// one.
/// </para>
/// </summary>
public sealed class LogDecisionToolHost : IToolHost
{
    private readonly IDecisionLogger _decisionLogger;
    private readonly IRunRepositories? _repositories;
    private readonly List<PlanDecision> _decisions = new();
    // The master shares one host across concurrent sub-agents and the loop hooks read this list
    // mid-run; the await below is sandbox round-trips now, not local file IO, so the window
    // between two adds is wide enough to matter. GetDecisions hands back a copy for that reason.
    private readonly object _sync = new();

    public LogDecisionToolHost(IDecisionLogger decisionLogger, IRunRepositories? repositories)
    {
        _decisionLogger = decisionLogger;
        _repositories = repositories;
    }

    public IReadOnlyList<PlanDecision> GetDecisions()
    {
        lock (_sync) return [.. _decisions];
    }

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
        [Description("The repository the decision is about. Required when the run has more than one " +
                     "repository; omit it when there is one.")] string? repository = null,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<DecisionCategory>(category, ignoreCase: true, out var cat))
            return $"Error: invalid category '{category}'.";
        var pick = _repositories is null ? null : RunRepositoryPick.From(_repositories.Names(), repository);
        if (pick?.Error is not null) return pick.Error;
        var files = pick?.Name is { } name ? _repositories!.FilesOf(name) : null;
        var outcome = await _decisionLogger.LogAsync(files, cat, decision, ct);
        // 2026-09-19-c511b: a lost repository COPY is not the model's business — the decision is
        // recorded on the run either way, and the one run that was told rewrote the same decision
        // four times in six seconds because rewording is the only repair a model has for a tool it
        // cannot fix. A decision recorded NOWHERE is a different answer, and it gets one.
        if (outcome == DecisionLogOutcome.NotRecorded)
            return $"Decision NOT recorded — the run's decision log is unavailable: [{category}] {decision}";
        lock (_sync) _decisions.Add(new PlanDecision(category, decision));
        return $"Decision logged: [{category}] {decision}";
    }
}
