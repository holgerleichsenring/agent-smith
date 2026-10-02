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
        // 2026-10-01-7f7ac: the export download — its own client, no token, no redirects, so a
        // storage url can neither receive the token nor bounce the request to another host.
        services.AddHttpClient<IFigmaImageDownloader, FigmaImageDownloader>(client => client.Timeout = TimeSpan.FromSeconds(30))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        return services;
    }
}
