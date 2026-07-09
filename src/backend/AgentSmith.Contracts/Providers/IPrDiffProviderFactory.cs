using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Providers;

/// <summary>
/// p0167a: creates the platform-appropriate <see cref="IPrDiffProvider"/> for a
/// configured repo connection (GitHub / GitLab / Azure DevOps). Local repos have
/// no PR concept and throw.
/// </summary>
public interface IPrDiffProviderFactory
{
    IPrDiffProvider Create(RepoConnection repo);
}
