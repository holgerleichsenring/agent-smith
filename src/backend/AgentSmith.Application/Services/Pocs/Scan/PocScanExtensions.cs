using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Pocs.Commit;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Pocs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Application.Services.Pocs.Scan;

/// <summary>2026-11-23-933dc: the code scan gate and the source commit of a poc-deploy run, and the no-database default of
/// their store.</summary>
public static class PocScanExtensions
{
    public static IServiceCollection AddPocScan(this IServiceCollection services)
    {
        services.TryAddSingleton<IPocScanStore, NoPocScanStore>();
        services.AddTransient<PocsConfigSource>().AddSingleton<PocFindingIds>().AddSingleton<PocSecretRule>()
            .AddSingleton<PocVulnerabilityRule>().AddSingleton<PocBlockRules>().AddSingleton<PocManifestFiles>()
            .AddTransient<PocRevisionFiles>().AddSingleton<PocLockfiles>().AddSingleton<PocRequirementsFilter>()
            .AddSingleton<PocScannedLockfiles>().AddSingleton<PocOsvRun>().AddTransient<PocOsvReportReader>()
            .AddSingleton<OsvReportParser>().AddTransient<PocDependencyScan>().AddSingleton<PocScanPrompt>()
            .AddTransient<PocScanExplanation>().AddTransient<PocCodeScanner>().AddTransient<PocOverruledPublisher>()
            .AddTransient<PocScanGate>().AddTransient<ICommandHandler<PocCodeScanContext>, PocCodeScanHandler>();
        return services.AddSingleton<PocGit>().AddTransient<PocSparseCheckout>().AddTransient<PocFolderReplace>()
            .AddTransient<PocFolderPush>().AddTransient<PocFolderWrite>()
            .AddTransient<ICommandHandler<PocSourceCommitContext>, PocSourceCommitHandler>();
    }
}
