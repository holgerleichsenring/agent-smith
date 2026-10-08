using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.Archive;
using Microsoft.Extensions.DependencyInjection.Extensions;
using AgentSmith.Server.Services.Lifecycle;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-01-283da: the reference-file store — its repository, and the copy of the legacy
/// image rows that the housekeeping leader drives until the legacy table is dropped.
/// </summary>
internal static class ReferenceFileExtensions
{
    internal static IServiceCollection AddReferenceFiles(this IServiceCollection services)
    {
        services.AddScoped<ReferenceFileRepository>();
        services.AddScoped<ReferenceSetRepository>(); // 2026-10-01-283db
        services.AddScoped<ReferenceUsageRepository>(); // 2026-10-08-e8b9g: the byte cap and duplicates
        services.AddScoped<ReferenceUploadDeletion>(); // 2026-10-08-e8b9g: one upload removed
        // 2026-10-01-283dc: a design turn's reference sandbox reads its set from here.
        services.RemoveAll<IReferenceSetReader>();
        services.AddSingleton<IReferenceSetReader, DbReferenceSetReader>();
        services.AddScoped<ReferenceNoteRepository>(); // 2026-10-02-075dd
        services.AddSingleton<IReferenceNotes, DbReferenceNotes>();
        services.AddScoped<LegacyAttachmentCopy>();
        // The copy switches identity insertion the way the archive import does; the archive
        // graph is not wired in every composition, so the two it needs are offered here too.
        services.TryAddSingleton<GeneratedKeyProperty>();
        services.TryAddSingleton<IdentityInsertSwitch>();
        services.AddSingleton<LegacyAttachmentCopySweeper>();
        return services;
    }
}
