using System.Net;

namespace AgentSmith.Application.Services.Browser;

/// <summary>2026-10-01-283de: <see cref="IHostAddressResolver"/> over the process's own resolver.</summary>
public sealed class DnsHostAddressResolver : IHostAddressResolver
{
    public Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken) =>
        Dns.GetHostAddressesAsync(host, cancellationToken);
}
