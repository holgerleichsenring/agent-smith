using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Sandbox;

/// <summary>
/// p0315b: builds lazy read-only <see cref="ISourceScopeSandbox"/> instances
/// for the repos of a spec-dialog scope. Creation is cheap (no container is
/// spawned) — materialisation happens inside the sandbox on first use.
/// </summary>
public interface ISourceScopeSandboxFactory
{
    ISourceScopeSandbox Create(ResolvedProject project, RepoConnection repo);
}
