using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Services.Sandbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AgentSmith.Infrastructure.Extensions;

public static class SandboxServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-process ISandbox implementation. Used by CLI mode,
    /// or as the default fallback in Server's auto-detected registration.
    /// Includes a placeholder <see cref="IOptions{TOptions}"/> of
    /// <see cref="SandboxGlobalConfig"/> so the AgentImageResolver does not
    /// fail-loud in InProcess mode where the spawned agent image is unused.
    /// Server (K8s/Docker) overrides this registration with one loaded from
    /// agentsmith.yml via AddSandboxGlobalConfig — last-registered wins.
    /// </summary>
    public static IServiceCollection AddInProcessSandbox(this IServiceCollection services)
    {
        services.AddSingleton<ISandboxFactory, InProcessSandboxFactory>();
        services.TryAddSingleton<IOptions<SandboxGlobalConfig>>(_ =>
            Options.Create(new SandboxGlobalConfig { AgentVersion = "in-process" }));
        return services;
    }
}
