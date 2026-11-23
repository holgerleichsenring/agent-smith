using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Pocs;

/// <summary>2026-11-23-933db: the PoC archive rules and the reader the upload and the intake call.</summary>
public static class PocArchiveRulesExtensions
{
    public static IServiceCollection AddPocArchiveRules(this IServiceCollection services)
    {
        services.AddSingleton<PocArchiveLayout>();
        services.AddSingleton<PocArchivePaths>();
        services.AddSingleton<PocEntryKinds>();
        services.AddSingleton<PocArchiveBounds>();
        services.AddSingleton<PocForbiddenNames>();
        services.AddSingleton<PocBinaryFiles>();
        services.AddSingleton<PocArchiveRules>();
        services.AddSingleton<DockerfileStages>();
        services.AddSingleton<DockerfileBlocklist>();
        services.AddSingleton<DockerfileUser>();
        services.AddSingleton<DockerfileExpose>();
        services.AddSingleton<DockerfileRules>();
        services.AddSingleton<PocRootDockerfile>();
        services.AddSingleton<PocArchiveInflater>();
        services.AddSingleton<PocArchiveReader>();
        return services;
    }
}
