using AgentSmith.Contracts.Models;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Audits project dependencies for known vulnerabilities.
/// Detects the package ecosystem and runs the appropriate audit tool.
/// </summary>
public interface IDependencyAuditor
{
    Task<DependencyAuditResult?> AuditAsync(string repoPath, CancellationToken cancellationToken);
}
