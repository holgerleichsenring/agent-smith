namespace AgentSmith.Infrastructure.Services.Providers.Source;

/// <summary>
/// The local provider's checkout-free reads: the WORKING TREE for the default reads — a local
/// repository is the operator's own checkout — and, since 2026-10-12-6bfaa, a NAMED branch through
/// git plumbing, because a branch's committed state is not in a working tree that has another
/// branch checked out. A path that is not a git repository has no branches, so every branch answer
/// is "absent".
/// </summary>
public sealed class LocalRepositoryRead(string basePath)
{
    public async Task<string?> TryReadFileAsync(string path, CancellationToken ct)
    {
        var full = Path.Combine(basePath, path);
        return File.Exists(full) ? await File.ReadAllTextAsync(full, ct) : null;
    }

    public IReadOnlyList<string> ListDirectory(string path)
    {
        var full = Path.Combine(basePath, path);
        return Directory.Exists(full)
            ? Directory.EnumerateFileSystemEntries(full).Select(Path.GetFileName).OfType<string>().ToList()
            : [];
    }

    public async Task<bool> BranchExistsAsync(string branch, CancellationToken ct) =>
        Directory.Exists(basePath)
        && (await LocalGitCommand.RunAsync(
            basePath, null, null, ct, "rev-parse", "--verify", "--quiet", $"refs/heads/{branch}")).Exit == 0;

    public async Task<string?> TryReadFileAsync(string path, string branch, CancellationToken ct)
    {
        if (!Directory.Exists(basePath)) return null;
        var result = await LocalGitCommand.RunAsync(basePath, null, null, ct, "show", $"{branch}:{Normalised(path)}");
        return result.Exit == 0 ? result.Out : null;
    }

    public async Task<IReadOnlyList<string>> ListDirectoryAsync(string path, string branch, CancellationToken ct)
    {
        if (!Directory.Exists(basePath)) return [];
        var result = await LocalGitCommand.RunAsync(
            basePath, null, null, ct, "ls-tree", "--name-only", $"{branch}:{Normalised(path)}");
        return result.Exit != 0
            ? []
            : result.Out.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static string Normalised(string path) => path.Replace('\\', '/').Trim('/');
}
