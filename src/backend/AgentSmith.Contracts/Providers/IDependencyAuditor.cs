using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Audits project dependencies for known vulnerabilities. Detects the package
/// ecosystem and runs the appropriate audit tool inside the sandbox via
/// Step{Kind=Run}; reads manifests via the sandbox reader.
/// </summary>
public interface IDependencyAuditor
{
    Task<DependencyAuditResult?> AuditAsync(
        ISandbox sandbox, ISandboxFileReader reader, CancellationToken cancellationToken);
}
