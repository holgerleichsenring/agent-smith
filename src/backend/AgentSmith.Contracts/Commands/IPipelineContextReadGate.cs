namespace AgentSmith.Contracts.Commands;

/// <summary>
/// Read-side interceptor for <see cref="PipelineContext"/>. PipelineExecutor
/// attaches a gate per step so every Get/TryGet is checked against the active
/// IPhaseDataFlow. Implementations decide what to do with undeclared reads
/// (throw or warn) based on the operator-set enforce flag.
/// </summary>
public interface IPipelineContextReadGate
{
    void OnRead(string key);
}
