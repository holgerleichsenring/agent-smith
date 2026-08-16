using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Providers.Agent;
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
/// <para>
/// p0427: and with the same RecordingChatClient, below the tool loop, so a traced harness
/// run records itself exactly as a production run does. Without it the harness could never
/// prove that what a run records is what a replay can re-drive.
/// </para>
/// </summary>
internal sealed class ScriptedChatClientFactoryAdapter(
    ScriptedChatClient client, IRunTraceWriter trace, IRunContextAccessor runContext)
    : IChatClientFactory
{
    private static readonly HashSet<TaskType> ToolBearingTasks =
        new() { TaskType.Primary, TaskType.Scout, TaskType.Planning };

    /// <summary>For the unit-shaped tests that drive one collaborator, not a whole run.</summary>
    public static ScriptedChatClientFactoryAdapter Untraced(ScriptedChatClient client) =>
        new(client, new NullRunTraceWriter(),
            new AgentSmith.Application.Services.Events.AsyncLocalRunContextAccessor());

    public IChatClient Create(AgentConfig agent, TaskType task, int? maxIterations = null, AgentSmith.Contracts.Providers.MasterLoopHooks? masterLoopHooks = null)
    {
        var inner = trace.IsEnabled
            ? new RecordingChatClient(client, trace, runContext)
            : (IChatClient)client;
        if (!ToolBearingTasks.Contains(task)) return inner;
        var iterations = maxIterations ?? 25;
        return new ChatClientBuilder(inner)
            .UseFunctionInvocation(configure: c => c.MaximumIterationsPerRequest = iterations)
            .Build();
    }

    public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 4096;
    public string GetModel(AgentConfig agent, TaskType task) => "scripted-fixture-model";
}
