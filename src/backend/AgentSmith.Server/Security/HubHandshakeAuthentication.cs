using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace AgentSmith.Server.Security;

/// <summary>
/// p0517: installs <see cref="HubHandshakeToken"/> in the authentication pipeline. This is
/// the one call p0503c could not make — the reader is a pure function over an
/// <c>HttpContext</c> precisely so it could exist before a JwtBearer registration did.
/// <para>
/// A browser cannot set an <c>Authorization</c> header on a websocket handshake, so
/// SignalR's own client puts the token in the query string instead. Setting
/// <see cref="MessageReceivedContext.Token"/> is what makes the handler validate it: with
/// nothing set the handler falls back to the header, finds none, and the connection is
/// anonymous.
/// </para>
/// <para>
/// The read also REWRITES the query string without the token, so nothing downstream of
/// authentication sees it. What already logged the raw query — the hosting diagnostics
/// line, the ingress access log, the browser's own URL — is untouched, and p0503c says so
/// in writing.
/// </para>
/// </summary>
internal static class HubHandshakeAuthentication
{
    internal static Task Receive(MessageReceivedContext context)
    {
        var token = HubHandshakeToken.Read(context.HttpContext);
        if (token is not null) context.Token = token;
        return Task.CompletedTask;
    }
}
