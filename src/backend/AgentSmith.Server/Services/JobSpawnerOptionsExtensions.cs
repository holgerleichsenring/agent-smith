using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// Registers <see cref="JobSpawnerOptions"/> via the IOptions&lt;T&gt; pattern.
/// Layered binding: legacy operator env-vars (K8S_NAMESPACE, AGENTSMITH_IMAGE,
/// IMAGE_PULL_POLICY, K8S_SECRET_NAME, DOCKER_NETWORK) are applied first as
/// defaults; the "JobSpawner" configuration section (appsettings.json or
/// JobSpawner__&lt;Key&gt; env-vars) overrides any value it sets. The combined
/// chain stays backwards-compatible with existing K8s deployments that wire the
/// legacy env-var names.
/// </summary>
public static class JobSpawnerOptionsExtensions
{
    public static IServiceCollection AddJobSpawnerOptions(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JobSpawnerOptions>(opts =>
        {
            opts.Namespace = Environment.GetEnvironmentVariable("K8S_NAMESPACE") ?? DispatcherDefaults.K8sNamespace;
            // AGENTSMITH_IMAGE is deprecated in p0137a — the canonical pinning point is
            // agentsmith.yml's top-level 'orchestrator.version' (and per-project overrides).
            // The env-var still binds for one release window; DeprecationWarningsLogger emits
            // the startup deprecation warning when set.
            opts.Image = Environment.GetEnvironmentVariable("AGENTSMITH_IMAGE") ?? string.Empty;
            opts.ImagePullPolicy = Environment.GetEnvironmentVariable("IMAGE_PULL_POLICY") ?? DispatcherDefaults.ImagePullPolicy;
            opts.SecretName = Environment.GetEnvironmentVariable("K8S_SECRET_NAME") ?? DispatcherDefaults.K8sSecretName;
            opts.DockerNetwork = Environment.GetEnvironmentVariable("DOCKER_NETWORK") ?? string.Empty;
        });
        services.Configure<JobSpawnerOptions>(configuration.GetSection("JobSpawner"));
        return services;
    }
}
