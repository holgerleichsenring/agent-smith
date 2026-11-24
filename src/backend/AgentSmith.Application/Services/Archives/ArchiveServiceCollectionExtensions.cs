using AgentSmith.Application.Services.Pocs;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Archives;

/// <summary>2026-11-23-933db: the archive rules the reference upload and the PoC import share, and the PoC archive
/// reader built on them.</summary>
public static class ArchiveServiceCollectionExtensions
{
    public static IServiceCollection AddArchiveRules(this IServiceCollection services)
    {
        services.AddSingleton<ReferencePathRule>();
        services.AddSingleton<ReferenceIgnoreList>();
        services.AddSingleton<ZipEntryChecksum>();
        services.AddSingleton<BoundedZipInflater>();
        services.AddPocArchiveRules();
        return services;
    }
}
