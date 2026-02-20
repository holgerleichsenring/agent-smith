using AgentSmith.Dispatcher.Adapters;
using AgentSmith.Dispatcher.Handlers;
using AgentSmith.Dispatcher.Services;
using AgentSmith.Infrastructure;
using Docker.DotNet;
using k8s;
using StackExchange.Redis;

namespace AgentSmith.Dispatcher.Extensions;

internal static class ServiceCollectionExtensions
{
    internal static IServiceCollection AddRedis(this IServiceCollection services)
    {
        var redisUrl = Environment.GetEnvironmentVariable("REDIS_URL") ?? DispatcherDefaults.RedisUrl;
        services.AddSingleton<IConnectionMultiplexer>(ConnectionMultiplexer.Connect(redisUrl));
        return services;
    }

    internal static IServiceCollection AddCoreDispatcherServices(this IServiceCollection services)
    {
        services.AddSingleton<IMessageBus, RedisMessageBus>();
        services.AddSingleton<ConversationStateManager>();
        services.AddSingleton<ChatIntentParser>();
        services.AddSingleton<MessageBusListener>();
        services.AddHostedService(sp => sp.GetRequiredService<MessageBusListener>());
        services.AddAgentSmithInfrastructure();
        return services;
    }

    internal static IServiceCollection AddIntentHandlers(this IServiceCollection services)
    {
        services.AddScoped<FixTicketIntentHandler>();
        services.AddScoped<ListTicketsIntentHandler>();
        services.AddScoped<CreateTicketIntentHandler>();
        services.AddScoped<SlackMessageDispatcher>();
        services.AddScoped<SlackInteractionHandler>();
        return services;
    }

    internal static IServiceCollection AddSlackAdapter(this IServiceCollection services)
    {
        services.AddHttpClient();
        services.AddSingleton(new SlackAdapterOptions
        {
            BotToken = Environment.GetEnvironmentVariable("SLACK_BOT_TOKEN") ?? string.Empty,
            SigningSecret = Environment.GetEnvironmentVariable("SLACK_SIGNING_SECRET") ?? string.Empty
        });
        services.AddSingleton<SlackAdapter>();
        services.AddSingleton<IPlatformAdapter>(sp => sp.GetRequiredService<SlackAdapter>());
        return services;
    }

    internal static IServiceCollection AddJobSpawnerOptions(this IServiceCollection services)
    {
        services.AddSingleton(new JobSpawnerOptions
        {
            Namespace = Environment.GetEnvironmentVariable("K8S_NAMESPACE") ?? DispatcherDefaults.K8sNamespace,
            Image = Environment.GetEnvironmentVariable("AGENTSMITH_IMAGE") ?? DispatcherDefaults.AgentImage,
            ImagePullPolicy = Environment.GetEnvironmentVariable("IMAGE_PULL_POLICY") ?? DispatcherDefaults.ImagePullPolicy,
            SecretName = Environment.GetEnvironmentVariable("K8S_SECRET_NAME") ?? DispatcherDefaults.K8sSecretName,
            TtlSecondsAfterFinished = 300,
            DockerNetwork = Environment.GetEnvironmentVariable("DOCKER_NETWORK") ?? string.Empty,
        });
        return services;
    }

    internal static async Task AddJobSpawnerAsync(
        this IServiceCollection services,
        ILogger logger)
    {
        var spawnerType = (Environment.GetEnvironmentVariable("SPAWNER_TYPE") ?? DispatcherDefaults.SpawnerType)
            .Trim().ToLowerInvariant();

        logger.LogInformation("Spawner type: {SpawnerType}", spawnerType);

        services.AddJobSpawnerOptions();

        if (spawnerType == DispatcherDefaults.SpawnerTypeDocker)
            await services.AddDockerSpawnerAsync(logger);
        else
            await services.AddKubernetesSpawnerAsync(logger);
    }

    private static async Task AddDockerSpawnerAsync(
        this IServiceCollection services,
        ILogger logger)
    {
        try
        {
            var uri = ResolveDockerUri();
            using var client = new DockerClientConfiguration(uri).CreateClient();
            await client.System.PingAsync();

            services.AddSingleton<IJobSpawner, DockerJobSpawner>();
            logger.LogInformation("Docker socket available — 'fix' commands enabled via DockerJobSpawner.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Docker socket not available: {Message}. " +
                "Mount /var/run/docker.sock into the dispatcher container.",
                ex.Message);
        }
    }

    private static async Task AddKubernetesSpawnerAsync(
        this IServiceCollection services,
        ILogger logger)
    {
        try
        {
            var k8sConfig = await BuildKubernetesConfigAsync();
            services.AddSingleton<IKubernetes>(new Kubernetes(k8sConfig));
            services.AddSingleton<IJobSpawner, KubernetesJobSpawner>();
            logger.LogInformation("Kubernetes available — 'fix' commands enabled via KubernetesJobSpawner.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Kubernetes not available: {Message}. " +
                "'fix' commands will be disabled. 'list' and 'create' still work.",
                ex.Message);
        }
    }

    private static async Task<KubernetesClientConfiguration> BuildKubernetesConfigAsync()
    {
        if (KubernetesClientConfiguration.IsInCluster())
            return KubernetesClientConfiguration.InClusterConfig();

        var kubeConfigPath = KubernetesClientConfiguration.KubeConfigDefaultLocation;
        var yaml = await File.ReadAllTextAsync(kubeConfigPath);

        // Docker Desktop exposes the K8s API at host.docker.internal, not 127.0.0.1
        yaml = yaml.Replace(DispatcherDefaults.K8sApiLocal, DispatcherDefaults.K8sApiPatch);

        using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(yaml));
        return KubernetesClientConfiguration.BuildConfigFromConfigFile(stream);
    }

    private static Uri ResolveDockerUri()
    {
        if (Environment.GetEnvironmentVariable("DOCKER_HOST") is { Length: > 0 } host)
            return new Uri(host);

        return OperatingSystem.IsWindows()
            ? new Uri(DispatcherDefaults.DockerSocketWindows)
            : new Uri(DispatcherDefaults.DockerSocketUnix);
    }
}
