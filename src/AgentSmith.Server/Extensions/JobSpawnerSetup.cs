using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using Docker.DotNet;
using k8s;

namespace AgentSmith.Server.Extensions;

internal static class JobSpawnerSetup
{
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
