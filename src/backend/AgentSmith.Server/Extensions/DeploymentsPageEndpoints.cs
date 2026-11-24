using System.Security.Claims;
using AgentSmith.Contracts.Deployment;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Security;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-11-23-8df6a: the Deployments page's read — the merged pull requests of the caller's runs.read projects with the
/// commit each merge landed as, and the deploy runs agent smith made, over the last ?days= (7 when absent). Read-only:
/// the merge commit is written by the pull-request refresh, never here.
/// </summary>
internal static class DeploymentsPageEndpoints
{
    private const int DefaultDays = 7; // not a limit: the window the page opens with when ?days= is absent

    internal static WebApplication MapDeploymentsPageEndpoints(this WebApplication app)
    {
        app.MapGet("/api/deployments", GetAsync).Needs(Permissions.RunsRead).InProject(ProjectFrom.AcrossProjects);
        return app;
    }

    // Internal so the endpoint tests drive it without a host.
    internal static async Task<IResult> GetAsync(int? days, ClaimsPrincipal user, CallerProjectScope scopes,
        PrReviewRecordRepository records, DeployRecordRepository deploys, TimeProvider clock, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var span = days ?? DefaultDays;
        if (span < 1 || span > (now - DateTimeOffset.MinValue).TotalDays)
            return Results.BadRequest($"days is a number of days from 1 back to the year 1; {span} is not.");
        var since = now.AddDays(-span);
        var scope = scopes.For(user, Permissions.RunsRead);
        return Results.Ok(new DeploymentsPageView(
            await records.MergedSinceAsync(scope, since, cancellationToken),
            await deploys.DeliveredSinceAsync(scope, since, cancellationToken)));
    }
}
