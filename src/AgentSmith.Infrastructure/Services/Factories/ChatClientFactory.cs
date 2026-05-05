using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Factories;

/// <summary>
/// IChatClientFactory implementation. Resolves AgentConfig.Type to the right
/// IChatClientBuilder, applies per-task ModelAssignment via IModelRegistry,
/// and wraps tool-bearing tasks with FunctionInvokingChatClient.
/// </summary>
public sealed class ChatClientFactory(
    AgentConfig agent,
    IModelRegistry modelRegistry,
    IEnumerable<IChatClientBuilder> builders,
    ILogger<ChatClientFactory> logger)
    : IChatClientFactory
{
    private static readonly HashSet<TaskType> ToolBearingTasks =
        new() { TaskType.Primary, TaskType.Scout, TaskType.Planning };

    private readonly Dictionary<string, IChatClientBuilder> _builderByType =
        BuildIndex(builders);

    public IChatClient Create(TaskType task)
    {
        var assignment = modelRegistry.GetModel(task);
        var effectiveType = assignment.ProviderType ?? agent.Type;

        if (!_builderByType.TryGetValue(effectiveType.ToLowerInvariant(), out var builder))
            throw new InvalidOperationException(
                $"No IChatClientBuilder registered for type='{effectiveType}'. " +
                $"Registered: [{string.Join(", ", _builderByType.Keys)}]");

        var bare = builder.Build(agent, assignment);

        logger.LogDebug(
            "Resolved IChatClient for {Task}: type={Type} model={Model} max={Max} tools={Tools}",
            task, effectiveType, assignment.Model, assignment.MaxTokens,
            ToolBearingTasks.Contains(task));

        return ToolBearingTasks.Contains(task)
            ? new ChatClientBuilder(bare).UseFunctionInvocation().Build()
            : bare;
    }

    public int GetMaxOutputTokens(TaskType task) => modelRegistry.GetModel(task).MaxTokens;

    public string GetModel(TaskType task) => modelRegistry.GetModel(task).Model;

    private static Dictionary<string, IChatClientBuilder> BuildIndex(IEnumerable<IChatClientBuilder> builders)
    {
        var map = new Dictionary<string, IChatClientBuilder>(StringComparer.OrdinalIgnoreCase);
        foreach (var builder in builders)
        {
            foreach (var type in builder.SupportedTypes)
                map[type] = builder;
        }
        return map;
    }
}
