using System.Net;
using AgentSmith.Contracts.Providers;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;

namespace AgentSmith.Infrastructure.Services.Providers.Tickets;

/// <summary>
/// 2026-10-14-dbcem: the CLASS of a failed requires link, from the exception the tracker's client threw. SDK and HTTP
/// messages carry organisation URLs, host names and response bodies, so only the class leaves the server — the rule
/// tracker reads follow. A status is a refusal; no status at all is a tracker that did not answer.
/// </summary>
internal static class RequiresLinkRefusal
{
    public const string NotAuthorized = "not authorized";
    public const string NoAnswer = "the tracker did not answer";

    public static RequiresLinkResult Of(Exception ex) => ex switch
    {
        VssUnauthorizedException => RequiresLinkResult.Refused(NotAuthorized),
        VssServiceResponseException { HttpStatusCode: var status } => ByStatus(status),
        Octokit.ApiException { StatusCode: var status } => ByStatus(status),
        HttpRequestException { StatusCode: { } status } => ByStatus(status),
        _ => RequiresLinkResult.Unreached(NoAnswer),
    };

    private static RequiresLinkResult ByStatus(HttpStatusCode status) => status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
        ? RequiresLinkResult.Refused(NotAuthorized)
        : RequiresLinkResult.Refused($"the tracker refused the link (HTTP {(int)status})");
}
