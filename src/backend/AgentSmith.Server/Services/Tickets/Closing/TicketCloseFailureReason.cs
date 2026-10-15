using System.Net;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets.Text;
using Microsoft.VisualStudio.Services.WebApi;

namespace AgentSmith.Server.Services.Tickets.Closing;

/// <summary>
/// 2026-10-15-114fa: the class of a failed close. A host that answered with a status other than 404, 401 or 403
/// declined the close (completed, locked, already merged): refused. Everything else is classed as a failed read.
/// </summary>
public static class TicketCloseFailureReason
{
    public static string Of(Exception ex) => StatusOf(ex) is { } status && !IsReadClass(status)
        ? TicketCloseReasons.Refused
        : TicketReadFailureReason.Of(ex);

    private static HttpStatusCode? StatusOf(Exception ex) => ex switch
    {
        Octokit.ApiException api => api.StatusCode,
        HttpRequestException { StatusCode: { } status } => status,
        VssServiceResponseException vss => vss.HttpStatusCode,
        _ => null,
    };

    private static bool IsReadClass(HttpStatusCode status) =>
        status is HttpStatusCode.NotFound or HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
}
