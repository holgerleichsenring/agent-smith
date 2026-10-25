using System.Net;
using AgentSmith.Application.Services.Browser;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-25-c5a7f: web_fetch's redirect follower. The primary handler follows no redirect,
/// so this one checks the first URL and every hop with <see cref="RenderUrlGuard"/> before it
/// is sent, and stops after <see cref="MaxRedirects"/>. A refusal is an HttpRequestException,
/// which the tool reports as its "Error: …" line. Behind a proxy a name the server cannot
/// resolve is left to the proxy; an address that resolves to a private range is still refused.
/// </summary>
public sealed class GuardedRedirectHandler(RenderUrlGuard guard, bool allowUnresolved) : DelegatingHandler
{
    public const int MaxRedirects = 5;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await RefuseUnlessPublicAsync(request.RequestUri!, cancellationToken);
        var response = await base.SendAsync(request, cancellationToken);
        for (var hop = 1; Location(response) is { } location; hop++)
        {
            response.Dispose();
            if (hop > MaxRedirects)
                throw new HttpRequestException($"refused: more than {MaxRedirects} redirects");
            request = NextHop(request, new Uri(request.RequestUri!, location));
            await RefuseUnlessPublicAsync(request.RequestUri!, cancellationToken);
            response = await base.SendAsync(request, cancellationToken);
        }
        return response;
    }

    private async Task RefuseUnlessPublicAsync(Uri url, CancellationToken cancellationToken)
    {
        if (await guard.RefusalForAsync(url, cancellationToken, allowUnresolved) is { } refusal)
            throw new HttpRequestException($"refused: {refusal}");
    }

    private static Uri? Location(HttpResponseMessage response) =>
        response.StatusCode is HttpStatusCode.MovedPermanently or HttpStatusCode.Found
            or HttpStatusCode.SeeOther or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect
            ? response.Headers.Location
            : null;

    private static HttpRequestMessage NextHop(HttpRequestMessage previous, Uri next)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, next);
        foreach (var header in previous.Headers)
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        return request;
    }
}
