using System.Security.Cryptography;
using System.Text;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// DI-friendly <see cref="IAgentSmithPaths"/> implementation. Cache-root
/// resolution delegates to <see cref="DefaultPaths"/> so the rules can't
/// drift between this class and <see cref="SkillsConfig.ResolveDefaultCacheDir"/>
/// (the pre-DI YAML-deserialization path). Per-project directories are keyed
/// by an 8-byte SHA-256 hash of the repo's remote URL — short enough to be
/// ergonomic, wide enough that collisions across a single operator's repos
/// are not a practical concern.
/// </summary>
public sealed class AgentSmithPaths : IAgentSmithPaths
{
    private const string ProjectsSubdir = "projects";

    public string CacheRoot { get; }

    public string SkillsCatalogRoot { get; }

    public AgentSmithPaths()
    {
        CacheRoot = DefaultPaths.ComputeCacheRoot();
        SkillsCatalogRoot = DefaultPaths.ComputeSkillsCatalogRoot();
    }

    public string ProjectCacheDir(string repositoryRemoteUrl) =>
        Path.Combine(CacheRoot, ProjectsSubdir, HashUrl(repositoryRemoteUrl));

    private static string HashUrl(string url)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(url.Trim()));
        return Convert.ToHexString(bytes.AsSpan(0, 8)).ToLowerInvariant();
    }
}
