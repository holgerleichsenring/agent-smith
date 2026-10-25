using System.Net;
using System.Net.Sockets;
using AgentSmith.Application.Services.Browser;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-25-c5a7f: the dial of a web_fetch that goes straight out. It resolves the host once,
/// refuses when any address is not public under <see cref="PublicAddressRule"/>, and connects
/// to the addresses it checked, so a second lookup cannot rebind the name to an internal one.
/// </summary>
public sealed class GuardedConnectCallback(IHostAddressResolver resolver, PublicAddressRule rule)
{
    public async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host.Trim('[', ']');
        var addresses = await CheckedAddressesAsync(host, cancellationToken);
        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }

    /// <summary>The addresses to dial; throws when the host resolves to none or to a non-public one.</summary>
    public async Task<IPAddress[]> CheckedAddressesAsync(string host, CancellationToken cancellationToken)
    {
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await resolver.ResolveAsync(host, cancellationToken);
        if (addresses.Length == 0) throw new HttpRequestException($"refused: '{host}' resolves to no address");
        foreach (var address in addresses)
            if (rule.RefusalFor(address) is { } refusal)
                throw new HttpRequestException($"refused: '{host}' is not a public address: {refusal}");
        return addresses;
    }
}
