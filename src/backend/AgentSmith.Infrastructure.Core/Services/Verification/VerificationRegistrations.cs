using AgentSmith.Contracts.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Infrastructure.Core.Services.Verification;

/// <summary>
/// 2026-08-30-0ea8: the published verification standard the binary ships, and the lens
/// that says which of its entries apply to a station of a request and how many of them
/// one station may be asked. Singletons: the checked-in export is parsed once per process
/// and the lens holds the classification of every id it carries.
/// </summary>
public static class VerificationRegistrations
{
    public static IServiceCollection AddVerificationCatalogue(this IServiceCollection services)
    {
        services.AddSingleton<AsvsFlatExportParser>();
        services.AddSingleton<VerificationLensTableParser>();
        services.AddSingleton<IVerificationCatalogue, EmbeddedVerificationCatalogue>();
        services.AddSingleton<IVerificationLens, AsvsVerificationLens>();
        return services;
    }
}
