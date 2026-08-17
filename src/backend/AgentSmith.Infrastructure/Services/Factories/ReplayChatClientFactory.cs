using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Factories;

/// <summary>
/// p0427: the chat-client factory of a REPLAYED run — one client for every task, wrapped
/// for tool-bearing tasks exactly as <see cref="ChatClientFactory"/> wraps a provider, so a
/// recorded tool call really invokes the registered tool.
/// <para>
/// It takes the client rather than building one: the replay source is a recorded run, and
/// the caller that loaded the recording is the one that owns it.
/// </para>
/// </summary>
public sealed class ReplayChatClientFactory(IChatClient replay, int defaultIterations = 25)
    : IChatClientFactory
{
    private static readonly HashSet<TaskType> ToolBearingTasks =
        [TaskType.Primary, TaskType.Scout, TaskType.Planning];

    public IChatClient Create(
        AgentConfig agent, TaskType task, int? maxIterations = null,
        MasterLoopHooks? masterLoopHooks = null) =>
        ToolBearingTasks.Contains(task)
            ? new ChatClientBuilder(replay)
                .UseFunctionInvocation(configure: c =>
                    c.MaximumIterationsPerRequest = maxIterations ?? defaultIterations)
                .Build()
            : replay;

    public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 4096;

    public string GetModel(AgentConfig agent, TaskType task) => "recorded-run-replay";
}
