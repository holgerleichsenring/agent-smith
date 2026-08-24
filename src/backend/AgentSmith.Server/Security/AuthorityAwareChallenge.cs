using AgentSmith.Server.Contracts;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace AgentSmith.Server.Security;

/// <summary>
/// p0503e: decides WHO a 401 blames. Measured against a stub authority taken down while a
/// server pointed at it: the refusal is <c>error="invalid_token"</c> with a description
/// calling the issuer invalid — the server telling a caller their token is bad because the
/// server cannot reach its own authority. That is the defect; the status code is not.
/// <para>
/// The status stays 401. A 503 would be a lie whenever cached signing keys are still
/// validating tokens — measured too: with a warm configuration an outage produces zero
/// failed requests — and its body is unreadable by the only consumer, since every dashboard
/// client throws away a non-ok body and keeps the status. The operator learns of this from
/// the finding the same probe records, which the degraded banner already polls for.
/// </para>
/// </summary>
internal sealed class AuthorityAwareChallenge(IAuthorityReachability reachability)
{
    // Not "invalid_token": a client that reads that code refreshes its token and comes
    // back, which against a dead authority is a loop. This is the code for a server that
    // is temporarily unable to handle the request, which is what is true.
    private const string Challenge =
        "Bearer error=\"temporarily_unavailable\", error_description=\""
        + "This server cannot reach its configured token authority, so it cannot validate "
        + "any token. The token was not rejected. See GET /api/config/findings.\"";

    internal Task WriteAsync(JwtBearerChallengeContext context)
    {
        // Reachable, or not yet known to be otherwise: the framework's own challenge is
        // then the honest one, and a forged token is still described as a forged token.
        if (!reachability.IsUnreachable) return Task.CompletedTask;
        context.HandleResponse();
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate = Challenge;
        return Task.CompletedTask;
    }
}
