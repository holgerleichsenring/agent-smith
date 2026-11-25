using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Scans source files against regex pattern definitions. Reads through
/// ISandboxFileReader so the scan walks the sandbox's tree under <c>root</c> (2026-11-25-9992c: the
/// sandbox's repository root, ISandbox.WorkDirectory()).
/// </summary>
public interface IStaticPatternScanner
{
    Task<StaticScanResult> ScanAsync(ISandboxFileReader reader, string root, CancellationToken cancellationToken);
}
