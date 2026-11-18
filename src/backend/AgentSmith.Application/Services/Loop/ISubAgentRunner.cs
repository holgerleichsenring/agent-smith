namespace AgentSmith.Application.Services.Loop;

/// <summary>
/// p0177: orchestrator for one master's spawned children. Takes the
/// SubAgentSpec list emitted by the LLM through <c>spawn_agents</c>,
/// caps in-flight count with SemaphoreSlim(MaxConcurrentSubAgents),
/// runs each child via <see cref="IAgenticLoopRunner"/>, and returns
/// the SubAgentResult list in deterministic spec order regardless of
/// completion order. No fail-fast: a failed child returns Status=Failed,
/// siblings keep running.
/// <para>2026-11-18-c8e2a: every child joins the context's team. <see cref="Start"/> runs children in the background
/// and <see cref="Continue"/> runs a finished child again over its conversation; each posts a notice to the team when
/// it finishes and hands its result to <c>finished</c>.</para>
/// </summary>
public interface ISubAgentRunner
{
    Task<IReadOnlyList<SubAgentResult>> RunAsync(
        IReadOnlyList<SubAgentSpec> specs,
        SubAgentContext context,
        CancellationToken cancellationToken);

    IReadOnlyList<SubAgentMember> Start(
        IReadOnlyList<SubAgentSpec> specs, SubAgentContext context,
        Func<SubAgentMember, SubAgentResult, Task> finished, CancellationToken cancellationToken);

    void Continue(
        SubAgentMember member, string message, SubAgentContext context,
        Func<SubAgentMember, SubAgentResult, Task> finished, CancellationToken cancellationToken);
}
