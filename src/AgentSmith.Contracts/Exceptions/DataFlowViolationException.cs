namespace AgentSmith.Contracts.Exceptions;

/// <summary>
/// Thrown by a gated PipelineContext read when the active step is not declared
/// as a consumer of the requested key under the resolved IPhaseDataFlow. Fatal
/// only when PipelineDataFlowConfig.Enforce is true; warning-only otherwise.
/// </summary>
public sealed class DataFlowViolationException : Exception
{
    public string ActivePhaseStep { get; }
    public string OffendingKey { get; }
    public IReadOnlyList<string> DeclaredFromSteps { get; }

    public DataFlowViolationException(
        string activePhaseStep,
        string offendingKey,
        IReadOnlyList<string> declaredFromSteps)
        : base(BuildMessage(activePhaseStep, offendingKey, declaredFromSteps))
    {
        ActivePhaseStep = activePhaseStep;
        OffendingKey = offendingKey;
        DeclaredFromSteps = declaredFromSteps;
    }

    private static string BuildMessage(string step, string key, IReadOnlyList<string> sources)
    {
        var sourceList = sources.Count == 0 ? "(none)" : string.Join(", ", sources);
        return $"Step '{step}' read undeclared context key '{key}'. " +
               $"Declared producers for this step: {sourceList}.";
    }
}
