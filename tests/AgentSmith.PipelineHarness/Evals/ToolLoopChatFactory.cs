using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// p0505: IChatClientFactory over ONE env-built client, wrapped the way production's
/// ChatClientFactory wraps a tool-bearing task — UseFunctionInvocation with the
/// caller's iteration cap. SingleClientChatFactory hands back the bare client, which
/// is enough for the drafter but would leave the analyzer's scout tools uncalled.
/// </summary>
internal sealed class ToolLoopChatFactory(IChatClient client, string modelId) : IChatClientFactory
{
    private const int DefaultMaxIterations = 25;

    public IChatClient Create(
        AgentConfig agent, TaskType task, int? maxIterations = null,
        MasterLoopHooks? masterLoopHooks = null) =>
        new ChatClientBuilder(client)
            .UseFunctionInvocation(configure: c =>
                c.MaximumIterationsPerRequest = maxIterations ?? DefaultMaxIterations)
            .Build();

    public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 4096;

    public string GetModel(AgentConfig agent, TaskType task) => modelId;
}
