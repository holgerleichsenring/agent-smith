using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Scans source files against regex pattern definitions. Reads through
/// ISandboxFileReader so the scan walks the sandbox /work tree.
/// </summary>
public interface IStaticPatternScanner
{
    Task<StaticScanResult> ScanAsync(ISandboxFileReader reader, CancellationToken cancellationToken);
}
