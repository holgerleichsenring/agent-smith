using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// p0329: the dashboard's READ surface for the expectation metric — the
/// p0328 ratification outcomes aggregated into expectation-hit-rate and
/// first-PR-acceptance per project, over time. Same shape as
/// <see cref="RunQueryEndpoints"/>: DB system-of-record via a scoped
/// repository, gated by the AGENTSMITH_UI_API_ENABLED block in Program.
/// </summary>
internal static class ExpectationMetricsEndpoints
{
    internal static WebApplication MapExpectationMetricsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/runs/expectations/metrics", GetMetricsAsync);
        return app;
    }

    private static async Task<IResult> GetMetricsAsync(
        ExpectationMetricsRepository expectations, CancellationToken cancellationToken)
    {
        var rows = await expectations.GetOutcomeRowsAsync(cancellationToken);
        return Results.Ok(ExpectationMetricsAggregator.Aggregate(rows));
    }
}
