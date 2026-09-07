using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Sandbox;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// Reads a repository's marker files and names the package ecosystem they declare —
/// the detection <see cref="IDependencyAuditor"/> used to carry inline, shared now
/// with every other reader that has to pick an ecosystem's own audit command.
/// </summary>
public interface IPackageEcosystemDetector
{
    /// <summary>Null when no supported marker file is present under the repository root.</summary>
    Task<PackageEcosystem?> DetectAsync(
        ISandboxFileReader reader, string repoPath, CancellationToken cancellationToken);
}
