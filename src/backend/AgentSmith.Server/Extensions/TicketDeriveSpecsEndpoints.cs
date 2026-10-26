using AgentSmith.Server.Models;
using AgentSmith.Server.Security;
using AgentSmith.Server.Services.Derivation;

namespace AgentSmith.Server.Extensions;

/// <summary>
/// 2026-10-26-bdd0e: a person derives a ticket's specs — stored on the ticket branch, or as a preview that writes
/// nothing. runs.control in the route's project, like every other act that starts a run on a ticket. 200 carries
/// the run id; every refusal is a plain sentence.
/// </summary>
internal static class TicketDeriveSpecsEndpoints
{
    public const string PreviewMode = "preview";

    internal static WebApplication MapTicketDeriveSpecsEndpoints(this WebApplication app)
    {
        app.MapPost("/api/tickets/{project}/{ticketId}/derive-specs", DeriveAsync)
            .Needs(Permissions.RunsControl).InProject(ProjectFrom.Route("project"));
        return app;
    }

    // Internal so the endpoint tests drive the real launcher without a host.
    internal static async Task<IResult> DeriveAsync(
        string project, string ticketId, DeriveSpecsRequest? request, SpecDerivationLauncher launcher,
        CancellationToken cancellationToken)
    {
        var preview = string.Equals(request?.Mode, PreviewMode, StringComparison.OrdinalIgnoreCase);
        var result = await launcher.LaunchAsync(
            TicketQueryEndpoints.Decoded(project), TicketQueryEndpoints.Decoded(ticketId), preview, cancellationToken);
        return result.Outcome switch
        {
            SpecDerivationOutcome.Started => Results.Ok(new DeriveSpecsResponse(result.RunId!)),
            SpecDerivationOutcome.NoCapacity => Results.Text(result.Reason, "text/plain", statusCode: StatusCodes.Status503ServiceUnavailable),
            SpecDerivationOutcome.UnknownProject => Results.Text(result.Reason, "text/plain", statusCode: StatusCodes.Status400BadRequest),
            _ => Results.Text(result.Reason, "text/plain", statusCode: StatusCodes.Status409Conflict),
        };
    }
}
