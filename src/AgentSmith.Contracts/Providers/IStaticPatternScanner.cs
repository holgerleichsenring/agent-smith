using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Scans source files against regex pattern definitions.
/// </summary>
public interface IStaticPatternScanner
{
    Task<StaticScanResult> ScanAsync(string repoPath, CancellationToken cancellationToken);
}
