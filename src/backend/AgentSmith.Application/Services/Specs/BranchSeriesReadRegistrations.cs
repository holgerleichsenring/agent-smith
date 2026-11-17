using AgentSmith.Application.Services.SpecDialog;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-17-a0b6b: the series reading — manifest lookup, spec files, join, composition — registered once
/// for every host that reads a ticket's specs: the run's derivation over a checkout and the server's ticket
/// readers over the branch. Idempotent, so both may call it.
/// </summary>
public static class BranchSeriesReadRegistrations
{
    public static IServiceCollection AddBranchSeriesRead(this IServiceCollection services)
    {
        services.TryAddTransient<SpecDraftReader>();
        services.TryAddTransient<SeriesManifest>();
        services.TryAddTransient<SeriesManifestFinder>();
        services.TryAddTransient<SeriesSpecFileReader>();
        services.TryAddTransient<SeriesJoin>();
        services.TryAddTransient<SeriesNewestManifest>();
        services.TryAddTransient<SeriesJoinOrder>();
        services.TryAddTransient<SubSeriesFiles>();
        services.TryAddTransient<SeriesComposer>();
        services.TryAddTransient<TicketBranchSeries>();
        return services;
    }
}
