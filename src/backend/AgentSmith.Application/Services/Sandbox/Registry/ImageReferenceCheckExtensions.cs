using AgentSmith.Application.Services.Tools;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Sandbox.Registry;

/// <summary>
/// 2026-10-12-50bdb: the registry lookup write_context_yaml asks before it writes a stack.image,
/// on its own named client — no redirect followed (the host is model-authored and judged
/// before each request), a bounded body. The process proxy settings apply as to every client.
/// </summary>
public static class ImageReferenceCheckExtensions
{
    public static IServiceCollection AddImageReferenceCheck(this IServiceCollection services)
    {
        services.AddHttpClient(RegistryTransport.ClientName,
                client => client.MaxResponseContentBufferSize = RegistryTransport.MaxResponseBytes)
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false });
        services.AddSingleton<RegistryImageReferenceParser>();
        services.AddSingleton<RegistryChallengeParser>();
        services.AddSingleton<RegistryTransport>();
        services.AddSingleton<RegistryTokenSource>();
        services.AddSingleton<RegistryClient>();
        services.AddSingleton<RegistryTagPageParser>();
        services.AddSingleton<RegistryTagLister>();
        services.AddSingleton<NearestTagRanker>();
        services.AddSingleton<IImageReferenceCheck, ImageReferenceCheck>();
        services.AddSingleton<ContextImageReview>();
        return services;
    }
}
