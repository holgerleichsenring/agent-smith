using System.Security.Cryptography;
using System.Text;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// XDG-compliant <see cref="IAgentSmithPaths"/> implementation. Honors
/// <c>XDG_CACHE_HOME</c> when set, falls back to <c>$HOME/.cache/agentsmith</c>.
/// Per-project directories are keyed by an 8-byte hash of the remote URL —
/// short enough to be ergonomic, wide enough that collisions across a single
/// operator's repos are not a practical concern.
/// </summary>
public sealed class AgentSmithPaths : IAgentSmithPaths
{
    private const string AppName = "agentsmith";

    public string CacheRoot { get; }

    public AgentSmithPaths()
    {
        var xdg = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
        var root = !string.IsNullOrEmpty(xdg)
            ? xdg
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".cache");
        CacheRoot = Path.Combine(root, AppName);
    }

    public string ProjectCacheDir(string repositoryRemoteUrl)
    {
        var key = HashUrl(repositoryRemoteUrl);
        return Path.Combine(CacheRoot, "projects", key);
    }

    private static string HashUrl(string url)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(url.Trim()));
        return Convert.ToHexString(bytes.AsSpan(0, 8)).ToLowerInvariant();
    }
}
