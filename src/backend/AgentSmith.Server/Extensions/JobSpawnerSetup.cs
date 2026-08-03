using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using Docker.DotNet;
using k8s;
using Microsoft.Extensions.Configuration;

namespace AgentSmith.Server.Extensions;

internal static class JobSpawnerSetup
{
    internal static async Task AddJobSpawnerAsync(
        this IServiceCollection services,
        IConfiguration configuration,
        ILogger logger)
    {
        var spawnerType = (Environment.GetEnvironmentVariable("SPAWNER_TYPE") ?? DispatcherDefaults.SpawnerType)
            .Trim().ToLowerInvariant();

        logger.LogInformation("Spawner type: {SpawnerType}", spawnerType);

        services.AddJobSpawnerOptions(configuration);

        var failure = spawnerType == DispatcherDefaults.SpawnerTypeDocker
            ? await services.AddDockerSpawnerAsync(logger)
            : await services.AddKubernetesSpawnerAsync(logger);

        // p0393: neither backend reachable used to register NOTHING. OrphanJobDetector is a
        // hosted service that takes an IJobSpawner, so the container could not build it and
        // StartAsync threw — the server died on an ABSENT dependency, which is exactly what
        // p0391a's ruling forbids. A spawner that refuses, and says why, always exists.
        if (failure is not null)
        {
            services.AddSingleton<IJobSpawner>(sp => new UnavailableJobSpawner(
                failure, sp.GetRequiredService<ILogger<UnavailableJobSpawner>>()));
        }
    }

    /// <summary>Returns null when the backend was registered, or the reason it was not.</summary>
    private static async Task<string?> AddDockerSpawnerAsync(
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
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Docker socket not available: {Message}. " +
                "Mount /var/run/docker.sock into the dispatcher container.",
                ex.Message);
            return $"Docker socket not available: {ex.Message}";
        }
    }

    /// <summary>Returns null when the backend was registered, or the reason it was not.</summary>
    private static async Task<string?> AddKubernetesSpawnerAsync(
        this IServiceCollection services,
        ILogger logger)
    {
        try
        {
            var k8sConfig = await BuildKubernetesConfigAsync();
            services.AddSingleton<IKubernetes>(new Kubernetes(k8sConfig));
            services.AddSingleton<IJobSpawner, KubernetesJobSpawner>();
            logger.LogInformation("Kubernetes available — 'fix' commands enabled via KubernetesJobSpawner.");
            return null;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                "Kubernetes not available: {Message}. " +
                "'fix' commands will be disabled. 'list' and 'create' still work.",
                ex.Message);
            return $"Kubernetes not available: {ex.Message}";
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
