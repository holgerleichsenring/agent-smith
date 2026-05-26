using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Scans git commit history for secrets that were committed and later deleted.
/// Runs git via Step{Kind=Run} inside the sandbox; reads tree snapshots via reader.
/// </summary>
public interface IGitHistoryScanner
{
    Task<GitHistoryScanResult> ScanAsync(
        ISandbox sandbox, ISandboxFileReader reader, CancellationToken cancellationToken);
}
