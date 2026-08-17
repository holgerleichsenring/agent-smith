using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Commands;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Application.Services.Scans;

/// <summary>
/// p0429: the scan pipelines' own registrations — the scanners' handlers, the finding
/// merge and delivery, and the accountability p0429 gave them: the contract a scan
/// states before it looks, the refutation every unvouched finding must survive, and the
/// coverage account the one delivery gate reads.
/// <para>
/// They left the pipeline-handler wall for the reason SpecDerivationRegistrations did: a
/// registration list that grows without bound is how a 220-line file stays 220 lines.
/// </para>
/// </summary>
public static class ScanRegistrations
{
    public static IServiceCollection AddScanPipelines(this IServiceCollection services)
    {
        services.AddTransient<ICommandHandler<CollectMasterFindingsContext>, CollectMasterFindingsHandler>();
        services.AddTransient<ICommandHandler<DeliverFindingsContext>, DeliverFindingsHandler>();
        services.AddTransient<ICommandHandler<StaticPatternScanContext>, StaticPatternScanHandler>();
        services.AddTransient<ICommandHandler<GitHistoryScanContext>, GitHistoryScanHandler>();
        services.AddTransient<ICommandHandler<DependencyAuditContext>, DependencyAuditHandler>();
        services.AddTransient<ICommandHandler<CompressSecurityFindingsContext>, CompressSecurityFindingsHandler>();
        services.AddTransient<ICommandHandler<MergeMasterFindingsContext>, MergeMasterFindingsHandler>();
        services.AddTransient<NucleiTopSelector>();
        services.AddTransient<ZapTopSelector>();
        services.AddTransient<SpectralTopSelector>();
        services.AddTransient<ICommandHandler<CompressApiScanFindingsContext>, CompressApiScanFindingsHandler>();
        services.AddTransient<ICommandHandler<SecurityTrendContext>, SecurityTrendHandler>();
        services.AddTransient<ICommandHandler<SecuritySnapshotWriteContext>, SecuritySnapshotWriter>();
        services.AddTransient<ICommandHandler<SpawnFixContext>, SpawnFixHandler>();
        return services.AddScanAccountability();
    }

    /// <summary>p0429: what the scan claims, what survives refutation, what went unanswered.</summary>
    private static IServiceCollection AddScanAccountability(this IServiceCollection services)
    {
        services.AddTransient<IScanContractCatalogue, ScanContractCatalogue>();
        services.AddTransient<IScanCoverageAccountant, ScanCoverageAccountant>();
        services.AddTransient<ICommandHandler<RatifyScanContractContext>, RatifyScanContractHandler>();
        services.AddTransient<ICommandHandler<AccountScanCoverageContext>, AccountScanCoverageHandler>();
        services.AddTransient<CitedCodeWindow>();
        services.AddTransient<RefutationVerdicts>();
        // p0429a: two evidence surfaces behind one routing factory — the source a repo
        // claim names, and the API document a live-target claim names.
        services.AddTransient<ScanEvidenceFactory>();
        services.AddTransient<SourceCitationResolver>();
        services.AddTransient<EndpointCitationResolver>();
        services.AddTransient<ICandidateFindingFactory, CandidateFindingFactory>();
        services.AddTransient<IFindingRefuter, FindingRefuter>();
        services.AddTransient<IFindingSubstantiator, FindingSubstantiator>();
        services.AddTransient<ICommandHandler<SubstantiateFindingsContext>, SubstantiateFindingsHandler>();
        return services;
    }
}
