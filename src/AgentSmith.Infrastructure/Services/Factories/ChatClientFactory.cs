using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Factories;

/// <summary>
/// IChatClientFactory implementation. Resolves AgentConfig.Type to the right
/// IChatClientBuilder, applies per-task ModelAssignment via a per-call
/// ConfigBasedModelRegistry, and wraps tool-bearing tasks with FunctionInvokingChatClient.
/// </summary>
public sealed class ChatClientFactory(
    IEnumerable<IChatClientBuilder> builders,
    ILoggerFactory loggerFactory)
    : IChatClientFactory
{
    private const int MaxIterationsPerRequest = 25;

    private static readonly HashSet<TaskType> ToolBearingTasks =
        new() { TaskType.Primary, TaskType.Scout, TaskType.Planning };

    private readonly Dictionary<string, IChatClientBuilder> _builderByType = BuildIndex(builders);
    private readonly ILogger<ChatClientFactory> _logger = loggerFactory.CreateLogger<ChatClientFactory>();

    public IChatClient Create(AgentConfig agent, TaskType task)
    {
        var assignment = GetAssignment(agent, task);
        var effectiveType = assignment.ProviderType ?? agent.Type;
        if (!_builderByType.TryGetValue(effectiveType.ToLowerInvariant(), out var builder))
            throw new InvalidOperationException(
                $"No IChatClientBuilder registered for type='{effectiveType}'. " +
                $"Registered: [{string.Join(", ", _builderByType.Keys)}]");

        var bare = builder.Build(agent, assignment);
        _logger.LogDebug(
            "Resolved IChatClient for {Task}: type={Type} model={Model} max={Max} tools={Tools}",
            task, effectiveType, assignment.Model, assignment.MaxTokens,
            ToolBearingTasks.Contains(task));

        return ToolBearingTasks.Contains(task)
            ? new ChatClientBuilder(bare)
                .UseFunctionInvocation(configure: c => c.MaximumIterationsPerRequest = MaxIterationsPerRequest)
                .Build()
            : bare;
    }

    public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => GetAssignment(agent, task).MaxTokens;
    public string GetModel(AgentConfig agent, TaskType task) => GetAssignment(agent, task).Model;

    private ModelAssignment GetAssignment(AgentConfig agent, TaskType task)
    {
        var registryConfig = agent.Models ?? BuildFallback(agent);
        var registry = new ConfigBasedModelRegistry(registryConfig, _logger);
        return registry.GetModel(task);
    }

    private static ModelRegistryConfig BuildFallback(AgentConfig agent)
    {
        var primary = new ModelAssignment { Model = agent.Model, Deployment = agent.Deployment };
        return new ModelRegistryConfig
        {
            Scout = primary, Primary = primary, Planning = primary,
            Reasoning = primary, Summarization = primary,
            ContextGeneration = primary, CodeMapGeneration = primary
        };
    }

    private static Dictionary<string, IChatClientBuilder> BuildIndex(IEnumerable<IChatClientBuilder> builders)
    {
        var map = new Dictionary<string, IChatClientBuilder>(StringComparer.OrdinalIgnoreCase);
        foreach (var builder in builders)
            foreach (var type in builder.SupportedTypes)
                map[type] = builder;
        return map;
    }
}
