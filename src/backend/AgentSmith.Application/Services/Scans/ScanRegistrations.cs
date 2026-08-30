using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Handlers;
using AgentSmith.Application.Services.Surface;
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
        return services.AddScanAccountability().AddSurfaceDifference();
    }

    /// <summary>p0429: what the scan claims, what survives refutation, what went unanswered.</summary>
    private static IServiceCollection AddScanAccountability(this IServiceCollection services)
    {
        services.AddTransient<IScanContractCatalogue, ScanContractCatalogue>();
        services.AddTransient<IScanCoverageAccountant, ScanCoverageAccountant>();
        services.AddTransient<ICommandHandler<RatifyScanContractContext>, RatifyScanContractHandler>();
        services.AddTransient<ICommandHandler<AccountScanCoverageContext>, AccountScanCoverageHandler>();
        // 2026-08-30-18e3: the entry map the scan master states, checked against its read set.
        services.AddTransient<StationMapResolver>();
        services.AddSingleton<Tools.ScanStationToolFactory>();
        services.AddTransient<ICommandHandler<AccountEntryStationsContext>, AccountEntryStationsHandler>();
        // 2026-08-30-3c12: the entries of the standard each station is asked, and the
        // answers the scan gave them, settled against the same read set.
        services.AddTransient<RequirementAccountant>();
        services.AddSingleton<Tools.RequirementAnswerRecorder>();
        services.AddSingleton<Tools.ScanRequirementToolFactory>();
        services.AddTransient<ICommandHandler<AccountRequirementAnswersContext>, AccountRequirementAnswersHandler>();
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

    /// <summary>
    /// 2026-08-30-c6ec: what the served interface offers that no declared first-party
    /// client exercises — the served description reduced to operations, the model that
    /// reads the client call sites, and the difference between the two.
    /// </summary>
    private static IServiceCollection AddSurfaceDifference(this IServiceCollection services)
    {
        services.AddTransient<IServedSurfaceReader, ServedSurfaceReader>();
        services.AddTransient<IClientSurfaceReader, ClientSurfaceReader>();
        services.AddTransient<ISurfaceDifferenceCalculator, SurfaceDifferenceCalculator>();
        services.AddTransient<
            ICommandHandler<AccountSurfaceDifferenceContext>, AccountSurfaceDifferenceHandler>();
        return services;
    }
}
