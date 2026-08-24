using Microsoft.AspNetCore.Http;

namespace AgentSmith.Server.Security;

/// <summary>
/// p0503c: lifts the handshake bearer token out of the hub's query string and drops it
/// from the request. A browser cannot set an Authorization header on a websocket
/// handshake, so SignalR's own client puts the token in <c>access_token</c> — which puts
/// a credential into every URL-shaped log on the path.
/// <para>
/// This is a pure function over <see cref="HttpContext"/> on purpose: it needs no
/// authentication package, so it can exist before the pipeline that will call it does.
/// *The hub refuses what the caller may not do* calls it from the JwtBearer
/// <c>OnMessageReceived</c> event in one line.
/// </para>
/// <para>
/// The rewrite cannot un-log what is already written: the hosting diagnostics
/// "Request starting" line carries the raw query before authentication runs, the ingress
/// access log carries it outside this repository entirely, and the browser keeps the URL.
/// It removes the token from everything that reads the query AFTER this point.
/// </para>
/// </summary>
internal static class HubHandshakeToken
{
    internal const string ParameterName = "access_token";

    /// <summary>The hub route, matched by segment so /negotiate is covered too.</summary>
    internal const string HubPath = "/hub";

    /// <summary>
    /// Returns the token carried on the hub path, or null when the path is not the hub's
    /// or no token is present. On a hit the request's query string is rewritten without
    /// the token and with every other parameter untouched.
    /// </summary>
    internal static string? Read(HttpContext context)
    {
        var request = context.Request;
        if (!request.Path.StartsWithSegments(HubPath)) return null;

        var token = request.Query[ParameterName].ToString();
        if (string.IsNullOrEmpty(token)) return null;

        request.QueryString = Without(request.Query);
        return token;
    }

    private static QueryString Without(IQueryCollection query)
    {
        var remaining = QueryString.Empty;
        foreach (var pair in query)
        {
            if (string.Equals(pair.Key, ParameterName, StringComparison.Ordinal)) continue;
            foreach (var value in pair.Value)
                remaining = remaining.Add(pair.Key, value ?? string.Empty);
        }
        return remaining;
    }
}
