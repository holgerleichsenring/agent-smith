using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using AgentSmith.Infrastructure.Services.RateLimiting;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Factories;

/// <summary>
/// IChatClientFactory implementation. Resolves AgentConfig.Type to the right
/// IChatClientBuilder, applies per-task ModelAssignment via a per-call
/// ConfigBasedModelRegistry, and wraps tool-bearing tasks with FunctionInvokingChatClient.
/// p0176b: every returned client is also wrapped with
/// <see cref="EventPublishingChatClient"/> so all consumers (not just
/// SkillCallRuntime) emit LlmCallStarted/Finished events with real cost.
/// </summary>
public sealed class ChatClientFactory(
    IEnumerable<IChatClientBuilder> builders,
    IEventPublisher eventPublisher,
    IRunContextAccessor runContext,
    IModelPricingResolver pricingResolver,
    ILlmRateLimiterRegistry rateLimiterRegistry,
    ILoggerFactory loggerFactory)
    : IChatClientFactory
{
    private const int MaxIterationsPerRequest = 25;

    private static readonly HashSet<TaskType> ToolBearingTasks =
        new() { TaskType.Primary, TaskType.Scout, TaskType.Planning };

    private readonly Dictionary<string, IChatClientBuilder> _builderByType = BuildIndex(builders);
    private readonly ILogger<ChatClientFactory> _logger = loggerFactory.CreateLogger<ChatClientFactory>();

    public IChatClient Create(AgentConfig agent, TaskType task, int? maxIterations = null)
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

        // p0188: rate-limiter wraps the bare provider client BEFORE event
        // publishing + function invocation so every call (master + sub-agent
        // + analyzer) shares the same per-(provider,model) budget. The
        // limiter blocks until both the requests-per-minute and the
        // input-tokens-per-minute budgets have capacity.
        var rateLimited = WrapWithRateLimit(bare, agent, assignment, effectiveType);

        // p0176b: wrap innermost with EventPublishingChatClient BEFORE
        // FunctionInvokingChatClient so each provider call (including
        // tool-loop iterations) produces its own LlmCallStarted/Finished
        // pair. Role / phase / repoName flow in via the ambient CallScope
        // on IRunContextAccessor (p0176a), opened by each handler around
        // its .GetResponseAsync invocation.
        var instrumented = new EventPublishingChatClient(
            rateLimited, eventPublisher, runContext, pricingResolver);

        // p0191: history-scrub sits above EventPublishing so the scrubbed
        // message list is what the provider sees. Prior-turn tool results
        // from sensitive tools become "[set, applied earlier turn]" — the
        // agent gets the credentials on the first iteration, the provider
        // never re-receives them on subsequent iterations.
        var scrubbed = new SensitiveToolHistoryScrubChatClient(instrumented);

        if (!ToolBearingTasks.Contains(task))
            return scrubbed;

        var iterations = maxIterations ?? MaxIterationsPerRequest;
        return new ChatClientBuilder(scrubbed)
            .UseFunctionInvocation(configure: c => c.MaximumIterationsPerRequest = iterations)
            .Build();
    }

    private IChatClient WrapWithRateLimit(
        IChatClient bare, AgentConfig agent, ModelAssignment assignment, string providerType)
    {
        var options = ResolveRateLimitOptions(agent, providerType);
        var modelKey = string.IsNullOrEmpty(assignment.Model) ? agent.Model : assignment.Model;
        var limiter = rateLimiterRegistry.GetOrCreate(providerType, modelKey ?? "default", options);
        var label = $"{providerType}/{modelKey}";
        return new RateLimitingChatClient(
            bare, limiter, label, loggerFactory.CreateLogger<RateLimitingChatClient>());
    }

    private static LlmRateLimitOptions ResolveRateLimitOptions(AgentConfig agent, string providerType)
    {
        var operatorOverride = agent.RateLimit;
        var defaults = DefaultRateFor(providerType, agent);
        return new LlmRateLimitOptions(
            RequestsPerMinute: operatorOverride?.RequestsPerMinute ?? defaults.RequestsPerMinute,
            InputTokensPerMinute: operatorOverride?.InputTokensPerMinute ?? defaults.InputTokensPerMinute);
    }

    // p0188: per-provider defaults. Subscription / OAuth tokens get a tight
    // budget; API-key tier defaults to Anthropic / OpenAI's published Tier 1
    // numbers. Operators override via AgentConfig.RateLimit.
    private static LlmRateLimitOptions DefaultRateFor(string providerType, AgentConfig agent)
    {
        var lower = providerType.ToLowerInvariant();
        if (lower is "claude" or "anthropic")
        {
            var apiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY") ?? string.Empty;
            return apiKey.StartsWith("sk-ant-oat", StringComparison.Ordinal)
                ? new LlmRateLimitOptions(RequestsPerMinute: 5, InputTokensPerMinute: 20_000)
                : new LlmRateLimitOptions(RequestsPerMinute: 50, InputTokensPerMinute: 40_000);
        }
        if (lower is "openai" or "azure_openai" or "azure-openai")
        {
            return new LlmRateLimitOptions(RequestsPerMinute: 60, InputTokensPerMinute: 60_000);
        }
        // Local / community providers — effectively unlimited.
        return new LlmRateLimitOptions(RequestsPerMinute: 600, InputTokensPerMinute: 600_000);
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
