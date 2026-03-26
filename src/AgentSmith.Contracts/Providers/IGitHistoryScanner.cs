using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Scans git commit history for secrets that were committed and later deleted.
/// </summary>
public interface IGitHistoryScanner
{
    Task<GitHistoryScanResult> ScanAsync(string repoPath, CancellationToken cancellationToken);
}
