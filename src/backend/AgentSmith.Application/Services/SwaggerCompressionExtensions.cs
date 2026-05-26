using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services;

/// <summary>
/// Swagger-spec compression service (p0147c). Stateless / threshold-gated, so
/// Singleton is safe — the compressor takes a logger only and reads the swagger
/// stream per call.
/// </summary>
public static class SwaggerCompressionExtensions
{
    public static IServiceCollection AddSwaggerCompression(this IServiceCollection services)
    {
        services.AddSingleton<ISwaggerSpecCompressor, SwaggerSpecCompressor>();
        return services;
    }
}
