using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// The dashboard's read surface for Criteria met: the criteria finished coding runs were
/// judged on, with the operator's overrules applied, per project and month. The route keeps
/// its expectation-era name so a bookmarked client finds it; the shape is
/// <see cref="Models.CriteriaMetSnapshot"/>.
/// </summary>
internal static class CriteriaMetEndpoints
{
    internal static WebApplication MapCriteriaMetEndpoints(this WebApplication app)
    {
        app.MapGet("/api/runs/expectations/metrics", GetMetricsAsync).Needs(Permissions.RunsRead);
        return app;
    }

    private static async Task<IResult> GetMetricsAsync(
        CriteriaMetRepository criteria, CriteriaMetAggregator aggregator, CancellationToken cancellationToken)
    {
        var runs = await criteria.GetJudgedCodeRunsAsync(cancellationToken);
        return Results.Ok(aggregator.Aggregate(runs));
    }
}
