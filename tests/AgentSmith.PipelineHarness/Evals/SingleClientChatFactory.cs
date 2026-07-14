using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// p0329: IChatClientFactory over ONE pre-built client — the eval tier builds
/// its client straight from env credentials (like the LiveLLM probe) and the
/// drafter only needs Create/GetMaxOutputTokens, so the full production
/// factory (registry, pricing, retry decoration) stays out of the eval loop.
/// </summary>
internal sealed class SingleClientChatFactory(IChatClient client, string modelId)
    : IChatClientFactory
{
    public IChatClient Create(AgentConfig agent, TaskType task, int? maxIterations = null) => client;
    public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 4096;
    public string GetModel(AgentConfig agent, TaskType task) => modelId;
}
