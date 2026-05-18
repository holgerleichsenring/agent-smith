using AgentSmith.Application;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Infrastructure;
using AgentSmith.Infrastructure.Services.Bus;
using AgentSmith.Infrastructure.Services.Dialogue;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Lifecycle;
using AgentSmith.Infrastructure.Services.Persistence;
using AgentSmith.Infrastructure.Services.Queue;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Contracts.Persistence;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Services.Handlers;
using AgentSmith.Server.Services.Hosting;
using AgentSmith.Server.Services.Webhooks;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;

namespace AgentSmith.Server.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddRedis(this IServiceCollection services)
    {
        var redisUrl = Environment.GetEnvironmentVariable("REDIS_URL") ?? DispatcherDefaults.RedisUrl;
        var multiplexer = ConnectionMultiplexer.Connect(redisUrl);
        services.AddSingleton<IConnectionMultiplexer>(multiplexer);
        services.AddSingleton<IRedisJobQueue, RedisJobQueue>();
        services.AddSingleton<IRedisClaimLock, RedisClaimLock>();
        services.AddSingleton<IRedisLeaderLease, RedisLeaderLease>();
        services.AddSingleton<IJobHeartbeatService, JobHeartbeatService>();
        services.AddSingleton<IConversationLookup, RedisConversationLookup>();
        services.AddSingleton<IDialogueTransport, RedisDialogueTransport>();
        services.AddSingleton<IRunArtifactStore, RedisRunArtifactStore>();
        return services;
    }

    /// <summary>
    /// Bundles the Server-side overrides on top of Application's CLI-safe defaults:
    /// (1) ITicketStatusTransitionerFactory rebinds to the locking variant for Jira;
    /// (2) IPipelineLifecycleCoordinator rebinds to the ticket-aware variant with
    ///     heartbeat support;
    /// (3) ITicketClaimService is registered (Server-only — depends on Redis services
    ///     that the CLI never carries).
    /// Must be called AFTER AddCoreDispatcherServices so the last-wins overrides
    /// stick against the bindings AddAgentSmithInfrastructure / AddAgentSmithCommands
    /// established.
    /// </summary>
    internal static IServiceCollection AddServerCompositionOverrides(this IServiceCollection services)
    {
        services.AddSingleton<ITicketStatusTransitionerFactory>(sp =>
            new LockingTicketStatusTransitionerFactory(
                sp.GetRequiredService<TicketStatusTransitionerFactory>(),
                sp.GetRequiredService<IRedisClaimLock>(),
                sp.GetRequiredService<ILoggerFactory>()));
        services.AddSingleton<IPipelineLifecycleCoordinator>(sp =>
            new TicketAwarePipelineLifecycleCoordinator(
                sp.GetRequiredService<ITicketStatusTransitionerFactory>(),
                sp.GetRequiredService<IJobHeartbeatService>(),
                sp.GetRequiredService<ILogger<TicketAwarePipelineLifecycleCoordinator>>()));
        // p0140b: ITicketClaimService is stateless; its deps (IRedisClaimLock,
        // ITicketStatusTransitionerFactory, IRedisJobQueue) are all singletons. Singleton
        // lifetime keeps the singleton WebhookSpawnDispatcher dependency chain valid.
        services.AddSingleton<ITicketClaimService, TicketClaimService>();
        return services;
    }

    internal static IServiceCollection AddCoreDispatcherServices(this IServiceCollection services)
    {
        services.AddSingleton<IMessageBus, RedisMessageBus>();
        services.AddSingleton<ConversationStateManager>();
        services.AddSingleton<ClarificationStateManager>();
        services.AddSingleton<ChatIntentParser>();
        services.AddSingleton<IBusMessageRouter, BusMessageRouter>();
        services.AddSingleton<MessageBusListener>();
        services.AddHostedService(sp => sp.GetRequiredService<MessageBusListener>());
        services.AddHostedService<OrphanJobDetector>();
        services.AddAgentSmithInfrastructure();
        services.AddAgentSmithCommands();
        services.AddIntentEngine();
        return services;
    }

    internal static IServiceCollection AddWebhookHandlers(this IServiceCollection services)
    {
        // p0140b: shared per-match spawn loop + zero-match handler used by all 8 ticket-event handlers.
        services.AddSingleton<WebhookSpawnDispatcher>();
        services.AddSingleton<IWebhookHandler, GitHubIssueWebhookHandler>();
        services.AddSingleton<IWebhookHandler, GitHubIssueCommentWebhookHandler>();
        services.AddSingleton<IWebhookHandler, GitHubPrLabelWebhookHandler>();
        services.AddSingleton<IWebhookHandler, GitHubPrCommentWebhookHandler>();
        services.AddSingleton<IWebhookHandler, GitLabIssueWebhookHandler>();
        services.AddSingleton<IWebhookHandler, GitLabIssueCommentWebhookHandler>();
        services.AddSingleton<IWebhookHandler, GitLabMrLabelWebhookHandler>();
        services.AddSingleton<IWebhookHandler, GitLabMrCommentWebhookHandler>();
        services.AddSingleton<IWebhookHandler, AzureDevOpsWorkItemWebhookHandler>();
        services.AddSingleton<IWebhookHandler, AzureDevOpsWorkItemCommentWebhookHandler>();
        services.AddSingleton<IWebhookHandler, AzureDevOpsPrCommentWebhookHandler>();
        services.AddSingleton<IWebhookHandler, JiraAssigneeWebhookHandler>();
        services.AddSingleton<IWebhookHandler, JiraCommentWebhookHandler>();
        return services;
    }

    internal static IServiceCollection AddLongRunningServices(this IServiceCollection services)
    {
        services.AddSingleton<QueueConsumerHostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<QueueConsumerHostedService>());
        services.AddSingleton<ISubsystemHealth>(sp =>
            sp.GetRequiredService<QueueConsumerHostedService>().Health);

        services.AddSingleton<HousekeepingLeaderHostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<HousekeepingLeaderHostedService>());
        services.AddSingleton<ISubsystemHealth>(sp =>
            sp.GetRequiredService<HousekeepingLeaderHostedService>().Health);

        services.AddSingleton<PollerLeaderHostedService>();
        services.AddHostedService(sp => sp.GetRequiredService<PollerLeaderHostedService>());
        services.AddSingleton<ISubsystemHealth>(sp =>
            sp.GetRequiredService<PollerLeaderHostedService>().Health);

        services.AddSingleton<RedisConnectionHealth>();
        services.AddSingleton<ISubsystemHealth>(sp =>
            sp.GetRequiredService<RedisConnectionHealth>().Health);

        return services;
    }

    private static IServiceCollection AddIntentEngine(this IServiceCollection services)
    {
        services.AddSingleton<ILlmIntentParser>(sp =>
        {
            var factory = sp.GetRequiredService<IChatClientFactory>();
            return new LlmIntentParser(
                factory,
                new AgentConfig { Type = "claude" },
                sp.GetRequiredService<ILogger<LlmIntentParser>>());
        });
        services.AddSingleton<IProjectResolver, ProjectResolver>();
        services.AddScoped<IntentEngine>();
        return services;
    }

    internal static IServiceCollection AddIntentHandlers(this IServiceCollection services)
    {
        services.AddScoped<FixTicketIntentHandler>();
        services.AddScoped<ListTicketsIntentHandler>();
        services.AddScoped<CreateTicketIntentHandler>();
        services.AddScoped<InitProjectIntentHandler>();
        services.AddScoped<HelpHandler>();
        services.AddScoped<SlackMessageDispatcher>();
        services.AddScoped<SlackErrorActionHandler>();
        services.AddScoped<SlackInteractionHandler>();
        services.AddScoped<SlackModalSubmissionHandler>();
        services.AddSingleton<CachedTicketSearch>();
        services.AddMemoryCache();
        return services;
    }

    internal static IServiceCollection AddTeamsAdapter(this IServiceCollection services)
    {
        var options = new TeamsAdapterOptions
        {
            AppId = Environment.GetEnvironmentVariable("TEAMS_APP_ID") ?? string.Empty,
            AppPassword = Environment.GetEnvironmentVariable("TEAMS_APP_PASSWORD") ?? string.Empty,
            TenantId = Environment.GetEnvironmentVariable("TEAMS_TENANT_ID") ?? string.Empty,
        };
        services.AddSingleton(options);
        services.AddSingleton(new TeamsJwtValidator(options.AppId));
        services.AddTransient<TeamsQuestionCardBuilder>();
        services.AddTransient<TeamsStatusCardBuilder>();
        services.AddTransient<TeamsCardBuilder>();
        // p0137b: typed HttpClients — IHttpClientFactory owns the handler pool.
        // 30 s timeout matches the previous default and aligns with the rest of
        // the platform-API call shape (Bot Framework + Teams). Service-URL is
        // resolved dynamically per conversation in TeamsApiClient (Bot Framework's
        // regional routing), so no BaseAddress is set here.
        services.AddHttpClient<BotFrameworkTokenProvider>(c => c.Timeout = TimeSpan.FromSeconds(30));
        services.AddHttpClient<TeamsApiClient>(c => c.Timeout = TimeSpan.FromSeconds(30));
        services.AddSingleton<TeamsTypedQuestionTracker>();
        services.AddSingleton<TeamsAdapter>();
        services.AddSingleton<IPlatformAdapter>(sp => sp.GetRequiredService<TeamsAdapter>());
        services.AddScoped<TeamsInteractionHandler>();
        return services;
    }

    internal static IServiceCollection AddSlackAdapter(this IServiceCollection services)
    {
        services.AddSingleton(new SlackAdapterOptions
        {
            BotToken = Environment.GetEnvironmentVariable("SLACK_BOT_TOKEN") ?? string.Empty,
            SigningSecret = Environment.GetEnvironmentVariable("SLACK_SIGNING_SECRET") ?? string.Empty
        });
        // p0137b: typed HttpClient for Slack — IHttpClientFactory-managed handler pool,
        // 30 s default timeout for slack.com/api/* calls.
        services.AddHttpClient<SlackApiClient>(c =>
        {
            c.BaseAddress = new Uri("https://slack.com/api/");
            c.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddTransient<SlackTypedQuestionBlockBuilder>();
        services.AddTransient<SlackMessageBlockBuilder>();
        services.AddTransient<SlackProgressFormatter>();
        services.AddSingleton<SlackAdapter>();
        services.AddSingleton<IPlatformAdapter>(sp => sp.GetRequiredService<SlackAdapter>());
        return services;
    }

    /// <summary>
    /// Registers <see cref="JobSpawnerOptions"/> via the IOptions&lt;T&gt; pattern.
    /// Layered binding: legacy operator env-vars (K8S_NAMESPACE, AGENTSMITH_IMAGE,
    /// IMAGE_PULL_POLICY, K8S_SECRET_NAME, DOCKER_NETWORK) are applied first as
    /// defaults; the "JobSpawner" configuration section (appsettings.json or
    /// JobSpawner__&lt;Key&gt; env-vars) overrides any value it sets. The combined
    /// chain stays backwards-compatible with existing K8s deployments that wire
    /// the legacy env-var names.
    /// </summary>
    internal static IServiceCollection AddJobSpawnerOptions(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JobSpawnerOptions>(opts =>
        {
            opts.Namespace = Environment.GetEnvironmentVariable("K8S_NAMESPACE") ?? DispatcherDefaults.K8sNamespace;
            // AGENTSMITH_IMAGE is deprecated in p0137a — the canonical pinning point
            // is agentsmith.yml's top-level 'orchestrator.version' (and per-project
            // overrides). The env-var still binds for one release window; a startup
            // deprecation warning is emitted when set (see DeprecationWarningsLogger).
            opts.Image = Environment.GetEnvironmentVariable("AGENTSMITH_IMAGE") ?? string.Empty;
            opts.ImagePullPolicy = Environment.GetEnvironmentVariable("IMAGE_PULL_POLICY") ?? DispatcherDefaults.ImagePullPolicy;
            opts.SecretName = Environment.GetEnvironmentVariable("K8S_SECRET_NAME") ?? DispatcherDefaults.K8sSecretName;
            opts.DockerNetwork = Environment.GetEnvironmentVariable("DOCKER_NETWORK") ?? string.Empty;
        });
        services.Configure<JobSpawnerOptions>(configuration.GetSection("JobSpawner"));
        return services;
    }
}
