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
    ILoggerFactory loggerFactory)
    : IChatClientFactory
{
    private const int MaxIterationsPerRequest = 25;

    private static readonly HashSet<TaskType> ToolBearingTasks =
        new() { TaskType.Primary, TaskType.Scout, TaskType.Planning };

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
        var instrumented = new EventPublishingChatClient(
            resilient, eventPublisher, runContext, pricing, assignment.Model ?? "");

        // p0191: history-scrub sits above EventPublishing so the scrubbed
        // message list is what the provider sees. Prior-turn tool results
        // from sensitive tools become "[set, applied earlier turn]" — the
        // agent gets the credentials on the first iteration, the provider
        // never re-receives them on subsequent iterations.
        var scrubbed = new SensitiveToolHistoryScrubChatClient(instrumented);

        if (!ToolBearingTasks.Contains(task))
            return scrubbed;

        // p0341c/p0341d: for the coding master's open loop, insert (innermost first) the
        // compaction middleware then the governor, both BELOW UseFunctionInvocation so they
        // re-enter on every tool iteration. Chain: FIC -> governor (budget fence + reminder)
        // -> compactor (thread-preserving in-flight reduction) -> provider. Null hooks keep
        // the plain chain (sub-agents, scan/planning calls).
        IChatClient loopInner = scrubbed;
        if (masterLoopHooks?.Compaction is { IsEnabled: true } compaction)
            loopInner = new CompactingChatClient(
                loopInner, compaction, masterLoopHooks,
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
            var prompt = new List<ChatMessage>
            {
                new(ChatRole.System, CompactionSummaryPrompt),
                new(ChatRole.User, SerializeForSummary(middle)),
            };
            var response = await summarizer.GetResponseAsync(
                prompt, new ChatOptions { MaxOutputTokens = 1024 }, ct);
            return response.Text ?? string.Empty;
        };
    }

    // p0362: the summary must carry the CONCLUSION drawn from each file, not just its
    // name. "read WolverineExtension.cs" forces a re-read to recover what it said;
    // "WolverineExtension.cs defines the naming contract as X" does not. The re-read
    // spiral is the conclusion getting dropped — one level below the ticket-paraphrase
    // failure p0357 pinned away.
    private const string CompactionSummaryPrompt =
        "You are a context compactor for a coding agent's conversation. Summarize the "
        + "messages below, preserving: for each file read or modified, its path AND the "
        + "load-bearing conclusion the agent drew from it (the contract, API shape, "
        + "invariant, or fact it went looking for — 'X defines Y', never just 'read X'); "
        + "key decisions and their reasoning; error messages and how they were resolved; "
        + "and the current state of the implementation. Omit raw file contents, redundant "
        + "tool call/result pairs, and verbose command output (note only the outcome). "
        + "The agent must not need to re-read a file merely to recover a conclusion this "
        + "summary dropped. Be concise but complete — this summary continues the work.";

    private static string SerializeForSummary(IReadOnlyList<ChatMessage> messages)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var m in messages)
        {
            var role = m.Role == ChatRole.Assistant ? "Assistant"
                : m.Role == ChatRole.Tool ? "Tool"
                : m.Role == ChatRole.System ? "System" : "User";
            foreach (var c in m.Contents)
            {
                switch (c)
                {
                    case TextContent t when !string.IsNullOrEmpty(t.Text):
                        sb.Append('[').Append(role).Append("] ").AppendLine(t.Text);
                        break;
                    case FunctionCallContent call:
                        sb.Append("[Assistant] called ").AppendLine(call.Name);
                        break;
                    case FunctionResultContent result:
                        var text = result.Result?.ToString() ?? string.Empty;
                        if (text.Length > 2000) text = text[..2000] + " …[truncated]";
                        sb.Append("[Tool result] ").AppendLine(text);
                        break;
                }
            }
        }
        return sb.ToString();
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
