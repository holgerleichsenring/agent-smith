using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.PipelineHarness.Llm;

/// <summary>
/// p0199: bridges the harness's single <see cref="ScriptedChatClient"/>
/// instance into the production IChatClientFactory shape. Tool-bearing
/// tasks (Primary / Scout / Planning) are wrapped with the same
/// FunctionInvokingChatClient as production's ChatClientFactory so
/// scripted FunctionCallContent responses actually invoke the registered
/// AITools — that's what exercises FilesystemToolHost / LogDecisionToolHost
/// end-to-end inside the master loop.
/// </summary>
internal sealed class ScriptedChatClientFactoryAdapter(ScriptedChatClient client) : IChatClientFactory
{
    private static readonly HashSet<TaskType> ToolBearingTasks =
        new() { TaskType.Primary, TaskType.Scout, TaskType.Planning };

    public IChatClient Create(AgentConfig agent, TaskType task, int? maxIterations = null, AgentSmith.Contracts.Providers.MasterLoopHooks? masterLoopHooks = null)
    {
        if (!ToolBearingTasks.Contains(task)) return client;
        var iterations = maxIterations ?? 25;
        return new ChatClientBuilder(client)
            .UseFunctionInvocation(configure: c => c.MaximumIterationsPerRequest = iterations)
            .Build();
    }

    public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 4096;
    public string GetModel(AgentConfig agent, TaskType task) => "scripted-fixture-model";
}
