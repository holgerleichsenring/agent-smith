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
    /// Tool-bearing tasks (Primary, Scout, Planning, Reasoning) are wrapped with
    /// FunctionInvokingChatClient. p0483 added Reasoning so the delivery account can be handed
    /// a tool: the wrapper is inert without one, since FunctionInvokingChatClient over empty
    /// ChatOptions.Tools makes the same single call, so the other Reasoning callers are
    /// unaffected.
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
    /// 2026-08-27-3eb1: same client, plus the compaction settings a caller wants applied
    /// to ITS tool loop — the coding master carries them on its hooks, every other loop
    /// (the analyzer's repository sweep first) had no way to ask. Honoured only when the
    /// resolved role states <c>context_window_tokens</c>: without a window there is no
    /// threshold to derive and the chain is the one built before. The default forwards to
    /// the four-argument overload, so a test double implements only what it needs.
    /// </summary>
    IChatClient Create(
        AgentConfig agent, TaskType task, int? maxIterations,
        MasterLoopHooks? masterLoopHooks, CompactionConfig? compaction)
        => Create(agent, task, maxIterations, masterLoopHooks);

    /// <summary>
    /// Returns the per-task max output tokens (from the agent's ModelRegistryConfig).
    /// </summary>
    int GetMaxOutputTokens(AgentConfig agent, TaskType task);

    /// <summary>
    /// Returns the model identifier for the given agent + task (for logging/cost tracking).
    /// </summary>
    string GetModel(AgentConfig agent, TaskType task);

    /// <summary>
    /// 2026-08-27-3eb1: the INPUT window stated for the given role, or null when the
    /// operator stated none. Null is the default answer so a test double stays valid.
    /// </summary>
    int? GetContextWindowTokens(AgentConfig agent, TaskType task) => null;
}
