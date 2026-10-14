using System.Net;
using AgentSmith.Domain.Exceptions;
using AgentSmith.Server.Models;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace AgentSmith.Server.Services.Tickets.Text;

/// <summary>
/// The class of a failed ticket read, from the exception the tracker's client threw. SDK and HTTP messages carry
/// organisation and host names, so only the class leaves the server.
/// </summary>
public static class TicketReadFailureReason
{
    // Azure DevOps answers a work item that does not exist, or that the token may not read, with this code.
    private const string MissingWorkItemCode = "TF401232";

    public static string Of(Exception ex) => ex switch
    {
        TicketNotFoundException => TicketTextReasons.NotFound,
        VssUnauthorizedException => TicketTextReasons.Unauthorized,
        VssServiceResponseException { HttpStatusCode: var status } when ByStatus(status) is { } reason => reason,
        VssServiceException vss when vss.Message.Contains(MissingWorkItemCode, StringComparison.Ordinal) => TicketTextReasons.NotFound,
        Octokit.ApiException { StatusCode: var status } when ByStatus(status) is { } reason => reason,
        HttpRequestException { StatusCode: { } status } when ByStatus(status) is { } reason => reason,
        _ => TicketTextReasons.Unreachable,
    };

    private static string? ByStatus(HttpStatusCode status) => status switch
    {
        HttpStatusCode.NotFound => TicketTextReasons.NotFound,
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => TicketTextReasons.Unauthorized,
        _ => null,
    };
}
