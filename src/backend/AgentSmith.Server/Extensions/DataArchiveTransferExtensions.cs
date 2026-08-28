using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Server.Services.Archive;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-08-28-3793: the server's half of the data archive. The writer and the reader come
/// from the persistence project unchanged — there is one archive format and one
/// implementation of it — and what the server adds is the policy for its OWN database plus
/// the two surfaces that carry a file over HTTP.
/// </summary>
internal static class DataArchiveTransferExtensions
{
    internal static IServiceCollection AddDataArchiveTransfer(this IServiceCollection services)
    {
        services.AddDataArchive();
        // The CLI's literal emptiness rule cannot hold here: this server has written rows
        // about itself before anyone can ask for a restore. ServerRestorePolicy states the
        // rule that can — no run has ever been recorded — and clears that bookkeeping.
        services.RemoveAll<IImportTargetPolicy>();
        services.AddSingleton<NoRecordedRunCheck>();
        services.AddSingleton<ServerBookkeepingReset>();
        services.AddSingleton<IImportTargetPolicy, ServerRestorePolicy>();
        services.AddSingleton<ArchiveUploadCeiling>();
        services.AddSingleton<ArchiveUploadSpool>();
        services.AddSingleton<ArchivePreviewReader>();
        services.AddSingleton<SynchronousResponseWrites>();
        services.AddSingleton<ArchiveDownload>();
        services.AddSingleton<ArchiveRestore>();
        return services;
    }
}
