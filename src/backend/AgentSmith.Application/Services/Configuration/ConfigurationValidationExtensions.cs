using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Configuration;

/// <summary>
/// Config-loaded validation + startup-time deprecation warners. Both are
/// stateless — Singleton keeps a single instance for the startup-time validation
/// + warning emission.
/// </summary>
public static class ConfigurationValidationExtensions
{
    public static IServiceCollection AddConfigurationValidation(this IServiceCollection services)
    {
        services.AddSingleton<AgentSmithConfigValidator>();
        services.AddSingleton<PollingConfigDeprecationWarner>();
        return services;
    }
}
