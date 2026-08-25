using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-08-25-0d01: everything needed to answer "which sandbox-agent image does this
/// project get". The build identity is registered here with TryAdd because the version is
/// now DERIVED from it, so every composition that resolves an image reference needs it —
/// not only the server composition that serves findings, which registers its own.
/// </summary>
public static class AgentImageResolutionExtensions
{
    public static IServiceCollection AddAgentImageResolution(this IServiceCollection services)
    {
        services.TryAddSingleton(_ => BuildIdentity.FromEnvironment());
        services.AddSingleton<IAgentVersionResolver, AgentVersionResolver>();
        services.AddSingleton<IAgentImageResolver, AgentImageResolver>();
        return services;
    }
}
