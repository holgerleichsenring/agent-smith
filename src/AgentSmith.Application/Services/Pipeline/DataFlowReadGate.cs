using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Exceptions;
using AgentSmith.Contracts.Pipeline;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Pipeline;

/// <summary>
/// IPipelineContextReadGate implementation backed by an IPhaseDataFlow.
/// Per-read check: looks up the active step's declared producers, allows the
/// read when the key is covered by any declared edge, otherwise either throws
/// (enforce=true) or logs Warning (enforce=false). Wildcard producer "*"
/// covers infrastructure-set keys (ResolvedPipeline, Sandbox, RunId, ActivePhaseStep).
/// </summary>
public sealed class DataFlowReadGate : IPipelineContextReadGate
{
    private readonly string _activeStep;
    private readonly IPhaseDataFlow _flow;
    private readonly bool _enforce;
    private readonly ILogger _logger;
    private readonly HashSet<string> _alreadyWarned = new(StringComparer.Ordinal);

    public DataFlowReadGate(
        string activeStep, IPhaseDataFlow flow, bool enforce, ILogger logger)
    {
        _activeStep = activeStep;
        _flow = flow;
        _enforce = enforce;
        _logger = logger;
    }

    public void OnRead(string key)
    {
        var declaredFromSteps = ResolveDeclaredFromSteps(key);
        if (declaredFromSteps.Count > 0) return;

        if (_enforce)
            throw new DataFlowViolationException(_activeStep, key, EnumerateAllDeclaredFromSteps());

        if (_alreadyWarned.Add(key))
            _logger.LogWarning(
                "DataFlow violation (warn-only): step {Step} read undeclared key {Key}",
                _activeStep, key);
    }

    private List<string> ResolveDeclaredFromSteps(string key)
    {
        var matches = new List<string>();
        foreach (var edge in _flow.Edges)
        {
            if (!EdgeAppliesToActiveStep(edge)) continue;
            if (EdgeCoversKey(edge, key))
                matches.Add(edge.FromPhaseStep);
        }
        return matches;
    }

    private static bool EdgeCoversKey(PhaseDataFlowEdge edge, string key)
        => edge.ContextKeys.Contains("*", StringComparer.Ordinal)
            || edge.ContextKeys.Contains(key, StringComparer.Ordinal);

    private List<string> EnumerateAllDeclaredFromSteps()
        => _flow.Edges
            .Where(EdgeAppliesToActiveStep)
            .Select(e => e.FromPhaseStep)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private bool EdgeAppliesToActiveStep(PhaseDataFlowEdge edge)
        => edge.ToPhaseStep == "*"
            || edge.ToPhaseStep.Equals(_activeStep, StringComparison.OrdinalIgnoreCase);
}
