using AgentSmith.Application.Models.Registry;

namespace AgentSmith.Application.Services.Registry;

/// <summary>
/// p0375: the load-bearing guard on the generic registry-auth path. The LLM
/// never sees a secret, so what it actually controls is the [{Path, Content}]
/// write set — this allowlist confines those writes to the sandbox user's home
/// config scope (the same scope the NuGet/npm fast-paths write). Absolute paths
/// outside the home, path traversal, repo working-tree paths and system paths
/// are rejected BEFORE any write — safety in the API, not the prompt.
/// </summary>
public sealed class RegistryAuthPathGuard
{
    private const string SandboxHome = "/root";
    private const string RepoTreeRoot = "/work";

    public RegistryAuthPathVerdict Check(string path)
    {
        var candidate = Normalize(path);
        if (candidate.Contains("..", StringComparison.Ordinal))
            return RegistryAuthPathVerdict.Reject($"path traversal ('..') in '{path}'");
        if (candidate.Contains('\\', StringComparison.Ordinal))
            return RegistryAuthPathVerdict.Reject($"backslash in '{path}' — sandbox paths are POSIX");
        if (IsRepoTree(candidate))
            return RegistryAuthPathVerdict.Reject(
                $"'{path}' is inside the repo working tree — auth config must never land in the checkout");
        if (!candidate.StartsWith(SandboxHome + "/", StringComparison.Ordinal))
            return RegistryAuthPathVerdict.Reject(
                $"'{path}' is outside the sandbox home config scope ({SandboxHome}/.<config>/...)");
        if (!candidate[(SandboxHome.Length + 1)..].StartsWith('.'))
            return RegistryAuthPathVerdict.Reject(
                $"'{path}' is not a dotfile/config path under the sandbox home — "
                + $"only {SandboxHome}/.<name> paths are stageable (the fast-path scope)");
        return RegistryAuthPathVerdict.Allow(candidate);
    }

    // "~/" is the one friendly normalization: it unambiguously means the
    // sandbox user's home, the exact scope this guard allows.
    private static string Normalize(string path)
    {
        var trimmed = path.Trim();
        return trimmed.StartsWith("~/", StringComparison.Ordinal)
            ? SandboxHome + trimmed[1..]
            : trimmed;
    }

    private static bool IsRepoTree(string candidate) =>
        candidate.Equals(RepoTreeRoot, StringComparison.Ordinal)
        || candidate.StartsWith(RepoTreeRoot + "/", StringComparison.Ordinal);
}
