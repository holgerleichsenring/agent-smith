using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Contracts.Providers;

/// <summary>2026-11-18-3863e: the pipeline runner of a repository's host, or null for a host that runs no pipeline
/// (a local working copy).</summary>
public interface IPipelineRunnerFactory
{
    IPipelineRunner? Create(RepoConnection repo);
}
