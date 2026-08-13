using AgentSmith.Server.Services.Catalog;
using AgentSmith.Server.Services.Diagnostics;
using AgentSmith.Server.Services.Events;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// The dashboard's read models: the run trail, the p0388b full-pipeline rail with its
/// decision notes, the per-step markdown artifacts, the catalog contents and the
/// connection diagnostics.
/// </summary>
internal static class DashboardReadersExtensions
{
    internal static IServiceCollection AddDashboardReaders(this IServiceCollection services)
    {
        services.AddSingleton<TrailReader>();
        services.AddSingleton<RunStepAggregatesReader>(); // p0404
        services.AddSingleton<RunStepsReader>();
        services.AddSingleton<RunDecisionsReader>();
        services.AddSingleton<ResultMarkdownReader>();
        services.AddSingleton<PlanMarkdownReader>();
        services.AddSingleton<SpecMarkdownReader>(); // p0390
        services.AddSingleton<AnalyzeMarkdownReader>();
        services.AddSingleton<CatalogContentsReader>();
        services.AddSingleton<IInfraConnectivityProbe, InfraConnectivityProbe>();
        services.AddSingleton<IChatConnectivityProbe, ChatConnectivityProbe>();
        services.AddSingleton<IConnectionDiagnosticsService, ConnectionDiagnosticsService>();
        return services;
    }
}
