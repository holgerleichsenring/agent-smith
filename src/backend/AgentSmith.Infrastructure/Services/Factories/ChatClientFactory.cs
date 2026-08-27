using System.Diagnostics;
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
    RateLimiting.ThrottleWaitReporter waitReporter,
    Contracts.Runs.IRunTraceWriter trace,
    CompactionSummaryRequest summaryRequest,
    WindowDerivedCompaction windowCompaction,
    ILoggerFactory loggerFactory)
    : IChatClientFactory
{
    private const int MaxIterationsPerRequest = 25;

    private static readonly HashSet<TaskType> ToolBearingTasks =
        new() { TaskType.Primary, TaskType.Scout, TaskType.Planning, TaskType.Reasoning };

    private readonly Dictionary<string, IChatClientBuilder> _builderByType = BuildIndex(builders);
    private readonly ILogger<ChatClientFactory> _logger = loggerFactory.CreateLogger<ChatClientFactory>();

    public async Task<ConnectionProbeResult> ProbeAsync(AgentConfig agent, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            // Cheapest task assignment + bare client (no rate-limit/events/tools, no run
            // context) — a 1-token request is enough to prove key + endpoint + deployment.
            var assignment = GetAssignment(agent, TaskType.Summarization);
            var effectiveType = assignment.ProviderType ?? agent.Type;
            if (!_builderByType.TryGetValue(effectiveType.ToLowerInvariant(), out var builder))
                return ConnectionProbeResult.Unreachable(
                    stopwatch.ElapsedMilliseconds, $"No client builder for type '{effectiveType}'");

            await builder.Build(agent, assignment).GetResponseAsync(
                [new ChatMessage(ChatRole.User, "ping")],
                new ChatOptions { MaxOutputTokens = 1 },
                cancellationToken);
            return ConnectionProbeResult.Reachable(stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Agent probe failed for model {Model}", agent.Model);
            return ConnectionProbeResult.Unreachable(stopwatch.ElapsedMilliseconds, ex.Message);
        }
    }

    public IChatClient Create(
        AgentConfig agent, TaskType task, int? maxIterations = null,
        MasterLoopHooks? masterLoopHooks = null)
        => Create(agent, task, maxIterations, masterLoopHooks, compaction: null);

    public IChatClient Create(
        AgentConfig agent, TaskType task, int? maxIterations,
        MasterLoopHooks? masterLoopHooks, CompactionConfig? compaction)
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

        // p0374: retry a transient network drop (a connection that dies mid-body —
        // HttpIOException "response ended prematurely") one layer OUTSIDE the throttle,
        // so each attempt re-acquires capacity. The SDK retries 429/5xx status codes
        // but not a mid-stream socket fault, which otherwise fails the whole run. Sits
        // BELOW EventPublishing so a retried call still emits exactly one LlmCall pair.
        var resilient = new TransientRetryChatClient(
            rateLimited, agent.Retry, assignment.Model ?? effectiveType,
            loggerFactory.CreateLogger<TransientRetryChatClient>());

        // p0176b: wrap innermost with EventPublishingChatClient BEFORE
        // FunctionInvokingChatClient so each provider call (including
        // tool-loop iterations) produces its own LlmCallStarted/Finished
        // pair. Role / phase / repoName flow in via the ambient CallScope
        // on IRunContextAccessor (p0176a), opened by each handler around
        // its .GetResponseAsync invocation.
        // p0274: layer this agent's pricing config over the default resolver so the
        // live per-call cost honours config-defined models (e.g. gpt-5.1), matching
        // the run-summary PipelineCostTracker. Without this the bare defaults-only
        // resolver can't price a config-only model → $0.0000 despite real tokens.
        var pricing = new OverlayModelPricingResolver(pricingResolver, agent.Pricing);
        var instrumented = new EventPublishingChatClient(resilient, eventPublisher, runContext,
            new LlmCallCostCalculator(pricing), waitReporter, assignment.Model ?? "");

        // p0427: a traced run records EVERY provider call here, below the tool loop, so the
        // record is a replayable sequence instead of one flattened entry per skill call —
        // and so the analyzer and the spec derivation are recorded too, not just the master.
        // Off by default: the wrapper is not inserted at all.
        var recorded = trace.IsEnabled
            ? new RecordingChatClient(instrumented, trace, runContext)
            : (IChatClient)instrumented;

        // p0191: history-scrub sits above EventPublishing so the scrubbed
        // message list is what the provider sees. Prior-turn tool results
        // from sensitive tools become "[set, applied earlier turn]" — the
        // agent gets the credentials on the first iteration, the provider
        // never re-receives them on subsequent iterations.
        var scrubbed = new SensitiveToolHistoryScrubChatClient(recorded);

        // 2026-08-27-3eb1: a context-length refusal names the role, the window it ran
        // against and the setting that would have prevented it — innermost, so nothing
        // above rewrites the provider's own words first.
        var diagnosed = new ContextLengthRefusalChatClient(
            scrubbed, task.ToString(), assignment.Model ?? effectiveType,
            assignment.ContextWindowTokens, _logger);

        if (!ToolBearingTasks.Contains(task))
            return diagnosed;

        // p0341c/p0341d: for the coding master's open loop, insert (innermost first) the
        // compaction middleware then the governor, both BELOW UseFunctionInvocation so they
        // re-enter on every tool iteration. Chain: FIC -> governor (budget fence + reminder)
        // -> compactor (thread-preserving in-flight reduction) -> provider. Null hooks keep
        // the plain chain (sub-agents, scan/planning calls).
        // 2026-08-27-3eb1: the same reduction now reaches any tool loop whose role states
        // a window — the scout sweep is ONE GetResponseAsync in which the function-invoking
        // client appends every tool result to one message list. The finalizer sits INSIDE
        // the compactor so it measures the view that is actually forwarded.
        IChatClient loopInner = diagnosed;
        if (assignment.ContextWindowTokens is { } window and > 0)
            loopInner = new ContextPressureFinalizingChatClient(
                loopInner, window, task.ToString(),
                loggerFactory.CreateLogger<ContextPressureFinalizingChatClient>());
        var effective = masterLoopHooks?.Compaction
            ?? windowCompaction.Derive(compaction, assignment.ContextWindowTokens);
        if (effective is { IsEnabled: true })
            loopInner = new CompactingChatClient(
                loopInner, effective, masterLoopHooks,
                BuildCompactionSummarizer(agent),
                loggerFactory.CreateLogger<CompactingChatClient>());
        if (masterLoopHooks is not null)
            loopInner = new MasterLoopGovernorChatClient(loopInner, masterLoopHooks);

        var iterations = maxIterations ?? MaxIterationsPerRequest;
        return new ChatClientBuilder(loopInner)
            .UseFunctionInvocation(configure: c => c.MaximumIterationsPerRequest = iterations)
            .Build();
    }

    // p0341d: the compactor's summarizer — a cheap, non-tool Summarization-task client
    // (fully instrumented: rate-limited, priced, event-emitting) built from the SAME agent.
    // It folds the evicted middle into a running summary; volume is low (one call per
    // compaction event, incremental thereafter).
    private Func<IReadOnlyList<ChatMessage>, CancellationToken, Task<string>> BuildCompactionSummarizer(
        AgentConfig agent)
    {
        var summarizer = Create(agent, TaskType.Summarization); // non-tool path — no recursion
        return async (middle, ct) =>
        {
            var response = await summarizer.GetResponseAsync(
                summaryRequest.Build(middle), new ChatOptions { MaxOutputTokens = 1024 }, ct);
            return response.Text ?? string.Empty;
        };
    }

    private IChatClient WrapWithRateLimit(
        IChatClient bare, AgentConfig agent, ModelAssignment assignment, string providerType)
    {
        var options = LlmRateBudget.For(agent, providerType);
        var modelKey = string.IsNullOrEmpty(assignment.Model) ? agent.Model : assignment.Model;
        var limiter = rateLimiterRegistry.GetOrCreate(providerType, modelKey ?? "default", options);
        var label = $"{providerType}/{modelKey}";
        return new RateLimitingChatClient(
            bare, limiter, label, waitReporter,
            loggerFactory.CreateLogger<RateLimitingChatClient>());
    }

    public int GetMaxOutputTokens(AgentConfig agent, TaskType task) => GetAssignment(agent, task).MaxTokens;
    public int? GetContextWindowTokens(AgentConfig agent, TaskType task) =>
        GetAssignment(agent, task).ContextWindowTokens;
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
