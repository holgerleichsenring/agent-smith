using AgentSmith.Application.Services.Browser;
using AgentSmith.Application.Services.Tools;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Tools;

/// <summary>
/// 2026-10-25-c5a7f: behind a proxy web_fetch keeps the proxy and the per-hop check is the
/// guard; without one it dials through the checked connect callback.
/// </summary>
[Trait("TestProcess", "env-2")]
[Collection(EnvVarCollection.Name)]
public sealed class WebToolHostHandlerTests : IDisposable
{
    private static readonly string[] ProxyVariables =
        ["HTTP_PROXY", "HTTPS_PROXY", "ALL_PROXY", "http_proxy", "https_proxy", "all_proxy"];

    private readonly Dictionary<string, string?> _previous =
        ProxyVariables.ToDictionary(name => name, Environment.GetEnvironmentVariable);
    private readonly WebToolHostHandler _handler =
        new(new GuardedConnectCallback(new DnsHostAddressResolver(), new PublicAddressRule()));

    [Fact]
    public void WebToolHostHandler_ProxyEnvironmentSet_KeepsProxyAndSkipsConnectCallback()
    {
        Environment.SetEnvironmentVariable("HTTPS_PROXY", "http://proxy.example:3128");

        using var handler = _handler.Build();

        handler.UseProxy.Should().BeTrue();
        handler.ConnectCallback.Should().BeNull();
        handler.AllowAutoRedirect.Should().BeFalse();
    }

    [Fact]
    public void WebToolHostHandler_NoProxy_DialsThroughTheCheckedCallback()
    {
        foreach (var name in ProxyVariables) Environment.SetEnvironmentVariable(name, null);

        using var handler = _handler.Build();

        handler.UseProxy.Should().BeFalse();
        handler.ConnectCallback.Should().NotBeNull();
        handler.AllowAutoRedirect.Should().BeFalse();
    }

    public void Dispose()
    {
        foreach (var (name, value) in _previous) Environment.SetEnvironmentVariable(name, value);
    }
}
