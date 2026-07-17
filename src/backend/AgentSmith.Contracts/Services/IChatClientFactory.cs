using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.AI;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// Resolves a Microsoft.Extensions.AI IChatClient for a given task type.
/// Replaces IAgentProviderFactory + IAgenticAnalyzerFactory + ILlmClientFactory.
/// AgentConfig is passed per-call (per-pipeline runtime data, not a DI singleton).
/// </summary>
public interface IChatClientFactory
{
    /// <summary>
    /// Read-only connectivity probe for one agent: a minimal 1-token round-trip on the
    /// bare provider client, to prove the key authenticates, the endpoint is reachable,
    /// and (Azure) the deployment exists. Unlike the repo/tracker probes this spends a
    /// tiny LLM call. Default returns "not supported" so test doubles need not implement
    /// it; the real ChatClientFactory overrides. Never throws — failures become Error.
    /// </summary>
    Task<ConnectionProbeResult> ProbeAsync(AgentConfig agent, CancellationToken cancellationToken)
        => Task.FromResult(ConnectionProbeResult.Unreachable(0, "probe not supported by this factory"));

    /// <summary>
    /// Returns the IChatClient configured for the given agent + task type.
    /// Tool-bearing tasks (Primary, Scout, Planning) are wrapped with FunctionInvokingChatClient.
    /// When <paramref name="maxIterations"/> is non-null, that value is used as the
    /// FunctionInvokingChatClient's MaximumIterationsPerRequest; null preserves the
    /// existing default (25). p0126a additive parameter for per-call cap support.
    /// p0341c: when <paramref name="masterLoopHooks"/> is non-null (the coding master's
    /// open loop), a governor DelegatingChatClient is inserted BELOW UseFunctionInvocation
    /// so it re-enters on every tool iteration — the within-pass money fence + the periodic
    /// ledger-reminder injection. Null keeps the plain chain (sub-agents, non-master calls).
    /// </summary>
    IChatClient Create(
        AgentConfig agent, TaskType task, int? maxIterations = null,
        MasterLoopHooks? masterLoopHooks = null);

    /// <summary>
    /// Returns the per-task max output tokens (from the agent's ModelRegistryConfig).
    /// </summary>
    int GetMaxOutputTokens(AgentConfig agent, TaskType task);

    /// <summary>
    /// Returns the model identifier for the given agent + task (for logging/cost tracking).
    /// </summary>
    string GetModel(AgentConfig agent, TaskType task);
}
