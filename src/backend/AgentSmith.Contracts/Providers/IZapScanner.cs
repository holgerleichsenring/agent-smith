using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Runs an OWASP ZAP security scan against a target URL.
/// </summary>
public interface IZapScanner
{
    Task<ZapResult> ScanAsync(ZapScanRequest request, CancellationToken cancellationToken);
}

public sealed record ZapScanRequest(
    string TargetUrl,
    string ScanType,
    string? SwaggerPath,
    string? AuthToken,
    int TimeoutSeconds = 300);
