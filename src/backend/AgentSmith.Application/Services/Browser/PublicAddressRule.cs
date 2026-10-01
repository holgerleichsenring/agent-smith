using System.Net;
using System.Net.Sockets;

namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283de: which addresses a render may reach — the public internet, and nothing of the
/// network the server and its sandboxes live in. An IPv4-mapped IPv6 address is unwrapped first,
/// so <c>::ffff:10.0.0.1</c> is judged as <c>10.0.0.1</c>. The same ranges are written into the
/// browser image's egress proxy (render.mjs), which judges every request the browser makes.
/// </summary>
public sealed class PublicAddressRule
{
    private static readonly (IPNetwork Range, string Name)[] Refused =
    [
        (IPNetwork.Parse("0.0.0.0/8"), "unspecified"),
        (IPNetwork.Parse("10.0.0.0/8"), "private"),
        (IPNetwork.Parse("100.64.0.0/10"), "carrier-grade NAT"),
        (IPNetwork.Parse("127.0.0.0/8"), "loopback"),
        (IPNetwork.Parse("169.254.0.0/16"), "link-local"),
        (IPNetwork.Parse("172.16.0.0/12"), "private"),
        (IPNetwork.Parse("192.0.0.0/24"), "IETF protocol assignment"),
        (IPNetwork.Parse("192.168.0.0/16"), "private"),
        (IPNetwork.Parse("198.18.0.0/15"), "benchmarking"),
        (IPNetwork.Parse("224.0.0.0/4"), "multicast"),
        (IPNetwork.Parse("240.0.0.0/4"), "reserved or broadcast"),
        (IPNetwork.Parse("::/96"), "unspecified, loopback or IPv4-compatible"),
        (IPNetwork.Parse("64:ff9b::/96"), "NAT64"),
        (IPNetwork.Parse("64:ff9b:1::/48"), "NAT64"),
        (IPNetwork.Parse("fc00::/7"), "unique-local"),
        (IPNetwork.Parse("fe80::/10"), "link-local"),
        (IPNetwork.Parse("ff00::/8"), "multicast"),
    ];

    /// <summary>Null when the address is public; otherwise the range that refuses it.</summary>
    public string? RefusalFor(IPAddress address)
    {
        ArgumentNullException.ThrowIfNull(address);
        var judged = address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
        if (judged.AddressFamily is not (AddressFamily.InterNetwork or AddressFamily.InterNetworkV6))
            return "not an IP address";
        foreach (var (range, name) in Refused)
            if (range.BaseAddress.AddressFamily == judged.AddressFamily && range.Contains(judged))
                return $"{judged} is {name} ({range})";
        return null;
    }
}
