using AgentSmith.Application.Services.Tools;
using AgentSmith.Contracts.Sandbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283de: registers render_reference — the egress pre-check, the browser sandbox it is
/// spawned in, the render and the tool's factory. The container-runtime fact defaults to "none":
/// only the server composition, which knows its backend, says a container can be spawned.
/// </summary>
public static class BrowserRenderExtensions
{
    public static IServiceCollection AddBrowserRender(this IServiceCollection services)
    {
        services.TryAddSingleton(new SandboxContainerRuntime(SpawnsContainers: false));
        services.AddSingleton<PublicAddressRule>();
        services.AddSingleton<IHostAddressResolver, DnsHostAddressResolver>();
        services.AddTransient<RenderUrlGuard>();
        services.AddTransient<RenderSourceParser>();
        services.AddTransient<BrowserSandboxOpener>();
        services.AddTransient<BrowserRenderInvocation>();
        services.AddTransient<ReferenceRenderer>();
        services.AddTransient<RenderResultText>();
        services.AddTransient<RenderReferenceServices>();
        services.AddTransient<RenderReferenceToolFactory>();
        return services;
    }
}
