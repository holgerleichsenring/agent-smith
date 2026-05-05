using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Services.Sandbox;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Extensions;

public static class SandboxServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-process ISandbox implementation. Used by CLI mode,
    /// or as the default fallback in Server's auto-detected registration.
    /// </summary>
    public static IServiceCollection AddInProcessSandbox(this IServiceCollection services)
    {
        services.AddSingleton<ISandboxFactory, InProcessSandboxFactory>();
        return services;
    }
}
