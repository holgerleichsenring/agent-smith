namespace AgentSmith.Application.Services.Pocs.Scan;

/// <summary>
/// 2026-11-23-933dc: the lockfiles and manifests osv-scanner reads by file name, in any folder of a PoC. Go is read from
/// go.mod (checked against the pinned scanner: it reads go.mod, not go.sum).
/// </summary>
public sealed class PocLockfiles
{
    private static readonly HashSet<string> Names = new(StringComparer.Ordinal)
    {
        "Pipfile.lock", "poetry.lock", "uv.lock", "pdm.lock", "package-lock.json", "yarn.lock", "pnpm-lock.yaml",
        "go.mod", "Cargo.lock", "composer.lock", "Gemfile.lock",
    };

    public bool IsLockfile(string path) => IsRequirements(path) || Names.Contains(FileName(path));

    /// <summary>A pip requirements file: requirements*.txt.</summary>
    public bool IsRequirements(string path)
    {
        var name = FileName(path);
        return name.StartsWith("requirements", StringComparison.Ordinal) && name.EndsWith(".txt", StringComparison.Ordinal);
    }

    private static string FileName(string path) => path[(path.LastIndexOf('/') + 1)..];
}
