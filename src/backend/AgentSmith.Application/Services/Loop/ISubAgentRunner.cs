namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// p0177: orchestrator for one master's spawned children. Takes the
/// SubAgentSpec list emitted by the LLM through <c>spawn_agents</c>,
/// caps in-flight count with SemaphoreSlim(MaxConcurrentSubAgents),
/// runs each child via <see cref="IAgenticLoopRunner"/>, and returns
/// the SubAgentResult list in deterministic spec order regardless of
/// completion order. No fail-fast: a failed child returns Status=Failed,
/// siblings keep running.
/// </summary>
public interface ISubAgentRunner
{
    Task<IReadOnlyList<SubAgentResult>> RunAsync(
        IReadOnlyList<SubAgentSpec> specs,
        SubAgentContext context,
        CancellationToken cancellationToken);
}
