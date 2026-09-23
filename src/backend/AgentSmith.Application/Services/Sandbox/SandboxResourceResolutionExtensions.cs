using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Sandbox;

/// <summary>
/// 2026-09-22-6c46: everything needed to answer "how big is this sandbox, and which layer
/// said so" — the layer ORDER and the acceptance of the LLM-authored block that one of its
/// layers reads. They are registered together because neither is usable without the other.
/// </summary>
public static class SandboxResourceResolutionExtensions
{
    public static IServiceCollection AddSandboxResourceResolution(this IServiceCollection services)
    {
        services.AddSingleton<IContextResourceAcceptance, ContextResourceAcceptance>();
        services.AddSingleton<ISandboxResourceResolver, SandboxResourceResolver>();
        return services;
    }
}
