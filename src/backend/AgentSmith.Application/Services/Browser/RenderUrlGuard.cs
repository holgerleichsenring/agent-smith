using System.Net;
using System.Net.Sockets;

namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283de: the server's half of the egress guard. A URL the model asks to render is
/// refused before any sandbox exists when it is not http(s), or when ANY address its host resolves
/// to is not public — a name with one public and one private record is refused, because the
/// browser's own lookup could land on either. The browser image's proxy judges again, request by
/// request, inside the sandbox; this check is what keeps a refused target from costing a spawn.
/// </summary>
public sealed class RenderUrlGuard(IHostAddressResolver resolver, PublicAddressRule rule)
{
    /// <summary>Null when the URL may be rendered; otherwise why not.</summary>
    public async Task<string?> RefusalForAsync(Uri url, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);
        if (!url.IsAbsoluteUri || url.Scheme is not ("http" or "https"))
            return $"only http and https URLs are rendered, not '{url.Scheme}'";
        var host = url.IdnHost.Trim('[', ']');
        var addresses = IPAddress.TryParse(host, out var literal) ? [literal] : await ResolveAsync(host, cancellationToken);
        if (addresses.Length == 0) return $"'{url.Host}' resolves to no address";
        foreach (var address in addresses)
            if (rule.RefusalFor(address) is { } refusal)
                return $"'{url.Host}' is not a public address: {refusal}";
        return null;
    }

    private async Task<IPAddress[]> ResolveAsync(string host, CancellationToken ct)
    {
        try { return await resolver.ResolveAsync(host, ct); }
        catch (SocketException) { return []; }
    }
}
