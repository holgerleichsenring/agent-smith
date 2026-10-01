using System.Net;
using AgentSmith.Application.Services.Browser;
using FluentAssertions;

namespace AgentSmith.Tests.Browser;

/// <summary>2026-10-01-283de: the server's pre-check of a URL, before anything spawns.</summary>
public sealed class RenderUrlGuardTests
{
    private readonly BrowserRenderFixture _fixture = new();
    private RenderUrlGuard Guard => new(_fixture, new PublicAddressRule());

    [Fact]
    public async Task RenderUrlGuard_HostWithOnePublicAndOnePrivateRecord_IsRefused()
    {
        _fixture.Hosts["split.test"] = [IPAddress.Parse("93.184.215.14"), IPAddress.Parse("10.0.0.7")];

        var refusal = await Guard.RefusalForAsync(new Uri("https://split.test/"), CancellationToken.None);

        refusal.Should().Contain("10.0.0.7", "the browser's own lookup could land on either record");
    }

    [Theory]
    [InlineData("http://127.0.0.1:6379/")]
    [InlineData("http://[::1]/")]
    [InlineData("http://[::ffff:a00:1]/")]
    [InlineData("http://0.0.0.0/")]
    [InlineData("http://2130706433/")]
    public async Task RenderUrlGuard_PrivateLiteral_IsRefusedWithoutALookup(string url)
    {
        (await Guard.RefusalForAsync(new Uri(url), CancellationToken.None)).Should().NotBeNull();
    }

    [Fact]
    public async Task RenderUrlGuard_PublicHost_IsAllowed_AndAnUnresolvableOneIsNot()
    {
        _fixture.Hosts["example.test"] = [IPAddress.Parse("93.184.215.14")];

        (await Guard.RefusalForAsync(new Uri("https://example.test/a"), CancellationToken.None)).Should().BeNull();
        (await Guard.RefusalForAsync(new Uri("https://nowhere.test/"), CancellationToken.None)).Should().Contain("no address");
    }

    [Fact]
    public async Task DnsHostAddressResolver_Localhost_ResolvesToLoopback()
    {
        var addresses = await new DnsHostAddressResolver().ResolveAsync("localhost", CancellationToken.None);

        addresses.Should().NotBeEmpty().And.OnlyContain(a => IPAddress.IsLoopback(a));
    }
}
