using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Runs a Nuclei security scan against an API target.
/// </summary>
public interface INucleiScanner
{
    Task<NucleiResult> ScanAsync(string targetUrl, string swaggerPath, CancellationToken cancellationToken);
}
