using System.Net;
using AgentSmith.Application.Services.Browser;
using FluentAssertions;

namespace AgentSmith.Tests.Browser;

/// <summary>
/// 2026-10-01-283de: the server's address rule — every range of the network the server and its
/// sandboxes live in is refused, in IPv4, IPv6 and IPv4-mapped IPv6; the public internet is not.
/// </summary>
public sealed class PublicAddressRuleTests
{
    private readonly PublicAddressRule _rule = new();

    [Theory]
    [InlineData("127.0.0.1")] [InlineData("127.255.0.9")] [InlineData("::1")]
    [InlineData("10.0.0.1")] [InlineData("172.16.0.1")] [InlineData("172.31.255.255")] [InlineData("192.168.1.10")]
    [InlineData("169.254.169.254")] [InlineData("fe80::1")]
    [InlineData("fc00::1")] [InlineData("fd12:3456::1")]
    [InlineData("100.64.0.1")] [InlineData("100.127.255.254")]
    [InlineData("0.0.0.0")] [InlineData("0.1.2.3")] [InlineData("::")]
    [InlineData("224.0.0.1")] [InlineData("ff02::1")] [InlineData("255.255.255.255")]
    [InlineData("::ffff:10.0.0.1")] [InlineData("::ffff:127.0.0.1")] [InlineData("::ffff:169.254.169.254")]
    [InlineData("::ffff:0.0.0.0")] [InlineData("::127.0.0.1")] [InlineData("64:ff9b::a00:1")]
    public void PublicAddressRule_LoopbackPrivateLinkLocalUlaCgnatUnspecified_AreRefused(string address)
    {
        _rule.RefusalFor(IPAddress.Parse(address)).Should().NotBeNull($"{address} is not on the public internet");
    }

    [Theory]
    [InlineData("93.184.215.14")] [InlineData("1.1.1.1")] [InlineData("172.32.0.1")] [InlineData("100.128.0.1")]
    [InlineData("2606:4700:4700::1111")] [InlineData("::ffff:93.184.215.14")]
    public void PublicAddressRule_PublicAddress_IsAllowed(string address)
    {
        _rule.RefusalFor(IPAddress.Parse(address)).Should().BeNull();
    }

    [Fact]
    public void PublicAddressRule_MappedAddress_IsJudgedAsTheIPv4ItCarries()
    {
        _rule.RefusalFor(IPAddress.Parse("::ffff:10.1.2.3")).Should().Contain("10.1.2.3").And.Contain("private");
    }
}
