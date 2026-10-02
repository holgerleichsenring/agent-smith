using AgentSmith.Contracts.Services;

namespace AgentSmith.Tests.Persistence;

/// <summary>2026-10-02-b540: a cache root under the temp directory for the disk discovery store.</summary>
internal sealed record DiscoveryTempPaths(string CacheRoot) : IAgentSmithPaths
{
    public string SkillsCatalogRoot => Path.Combine(CacheRoot, "skills");
    public string ProjectCacheDir(string repositoryRemoteUrl) => Path.Combine(CacheRoot, "projects");
}
