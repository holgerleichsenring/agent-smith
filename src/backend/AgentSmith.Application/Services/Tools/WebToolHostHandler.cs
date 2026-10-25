namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-25-c5a7f: web_fetch's primary handler. It follows no redirect itself (the
/// <see cref="GuardedRedirectHandler"/> does, hop by hop). With no proxy in the environment it
/// dials through <see cref="GuardedConnectCallback"/>; with one it keeps the proxy, because an
/// estate whose egress is proxy-only would lose web_fetch otherwise, and the proxy's own
/// resolution is the residual.
/// </summary>
public sealed class WebToolHostHandler(GuardedConnectCallback connect)
{
    private static readonly string[] ProxyVariables =
        ["HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "http_proxy", "https_proxy", "all_proxy"];

    public bool IsProxied() =>
        ProxyVariables.Any(name => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(name)));

    public SocketsHttpHandler Build()
    {
        var handler = new SocketsHttpHandler { AllowAutoRedirect = false };
        if (IsProxied()) return handler;
        handler.UseProxy = false;
        handler.ConnectCallback = connect.ConnectAsync;
        return handler;
    }
}
