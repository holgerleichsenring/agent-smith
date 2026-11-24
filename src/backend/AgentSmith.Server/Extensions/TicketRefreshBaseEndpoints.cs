using AgentSmith.Server.Models;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Branches;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-11-24-9dc0e: POST /api/tickets/{project}/{ticketId}/refresh-base {url} — Try again on a refused refresh, which
/// starts a refresh-base run (runs.control, like Retry). 200 with the run id; 404 an unknown project; 400 no URL; 409 a
/// refusal as the sentence that says why.
/// </summary>
internal static class TicketRefreshBaseEndpoints
{
    internal static WebApplication MapTicketRefreshBaseEndpoints(this WebApplication app)
    {
        app.MapPost("/api/tickets/{project}/{ticketId}/refresh-base", TryAgainAsync).Needs(Permissions.RunsControl).InProject(ProjectFrom.Route("project"));
        return app;
    }

    // Internal so the endpoint tests drive it without a host.
    internal static async Task<IResult> TryAgainAsync(string project, string ticketId, RefreshBaseRequest request,
        TicketProjects projects, BaseRefreshRetry retry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var (name, id) = (TicketQueryEndpoints.Decoded(project), TicketQueryEndpoints.Decoded(ticketId));
        if (projects.Of(name) is not { } resolved) return Say(StatusCodes.Status404NotFound, $"No project {name} is configured.");
        if (string.IsNullOrWhiteSpace(request.Url)) return Say(StatusCodes.Status400BadRequest, "The pull request's URL is required.");
        var started = await retry.TryAgainAsync(resolved, id, request.Url, cancellationToken);
        return started.RunId is { } runId ? Results.Ok(new { runId }) : Say(StatusCodes.Status409Conflict, started.Refusal!);
    }

    private static IResult Say(int status, string sentence) => Results.Text(sentence, "text/plain", statusCode: status);
}
