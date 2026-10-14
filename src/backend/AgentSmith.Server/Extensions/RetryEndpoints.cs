using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Server.Services.SpecDialog;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-13-9a59n: POST /api/runs/{runId}/retry — an unchanged attempt after an infrastructure end, started
/// through the rework queue with the caller as its cause. 404 an unknown run, 400 a run with nothing to retry,
/// 409 every refusal as one <see cref="RetryRefusalView"/>, 503 when the tracker could not be read, 202 requested.
/// </summary>
internal static class RetryEndpoints
{
    internal static WebApplication MapRetryEndpoints(this WebApplication app)
    {
        app.MapPost("/api/runs/{runId}/retry", RetryAsync).Needs(Security.Permissions.RunsControl);
        return app;
    }

    internal static async Task<IResult> RetryAsync(string runId, HttpContext ctx, RunRetry retry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ctx);
        var result = await retry.RetryAsync(
            runId, SpecDialogOwnership.PrincipalOf(ctx.User), RetryOrigin.Button, cancellationToken);
        return result.Kind switch
        {
            RunRetryResultKind.UnknownRun => Results.NotFound(),
            RunRetryResultKind.Invalid => Results.BadRequest(result.Text),
            RunRetryResultKind.Refused => Results.Conflict(result.Refusal),
            RunRetryResultKind.Unavailable => Results.Problem(result.Text, statusCode: StatusCodes.Status503ServiceUnavailable),
            _ => Results.Accepted(value: new { runId, ticketId = result.TicketId, retryRequested = true }),
        };
    }
}
