using System.Net;

namespace AgentSmith.Application.Services.Browser;

/// <summary>2026-10-01-283de: every address a host name resolves to, as the server sees it.</summary>
public interface IHostAddressResolver
{
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken);
}
