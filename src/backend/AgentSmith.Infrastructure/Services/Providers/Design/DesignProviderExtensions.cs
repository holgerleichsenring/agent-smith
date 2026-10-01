using AgentSmith.Contracts.Providers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Infrastructure.Services.Providers.Design;

/// <summary>2026-10-01-7f7ab: the design-tool clients — Figma's, a typed client on its API host.</summary>
public static class DesignProviderExtensions
{
    public static IServiceCollection AddDesignProviders(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddHttpClient<IFigmaClient, FigmaClient>(client =>
        {
            client.BaseAddress = FigmaClient.ApiHost;
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        return services;
    }
}
