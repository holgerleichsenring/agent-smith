using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.PipelineHarness.Llm;

/// <summary>
/// p0199: bridges the harness's single <see cref="ScriptedChatClient"/>
/// instance into the production IChatClientFactory shape. Every Create
/// call returns the same scripted instance — the test scripts responses
/// in order and reads the invocation log to assert tool-call shape.
/// </summary>
internal sealed class ScriptedChatClientFactoryAdapter(ScriptedChatClient client) : IChatClientFactory
{
    public IChatClient Create(AgentConfig agent, TaskType task, int? maxIterations = null) => client;
    public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => 4096;
    public string GetModel(AgentConfig agent, TaskType task) => "scripted-fixture-model";
}
