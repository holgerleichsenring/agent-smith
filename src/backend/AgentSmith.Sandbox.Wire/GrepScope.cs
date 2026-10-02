namespace AgentSmith.Sandbox.Wire;

/// <summary>
/// What a Grep step does NOT search, whichever engine serves it.
/// <para>
/// 2026-09-17-042ed: both search paths — ripgrep and the managed fallback — skip these
/// directories and anything over the size ceiling, so "the pattern is absent" means the same
/// thing on either. The list is stated once, here, because a reader who is told an absence
/// must be able to be told where it was not looked for: the tools that offer a search carry
/// <see cref="Summary"/> in their own description, which is a compile-time constant so that an
/// attribute can hold it and the two cannot drift.
/// </para>
/// </summary>
public static class GrepScope
{
    /// <summary>Directories neither search path descends into, as one readable list.</summary>
    public const string ExcludedDirsText =
        ".git, node_modules, bin, obj, .vs, .idea, dist, build, "
        + ".next, .nuxt, coverage, .terraform, vendor, __pycache__";

    /// <summary>The same list, one entry per element.</summary>
    public static readonly string[] ExcludedDirs = ExcludedDirsText.Split(", ");

    /// <summary>Files larger than this are not searched. <see cref="Summary"/> says "1 MB".</summary>
    public const long MaxFileSizeBytes = 1_000_000;

    /// <summary>The blind spot in words, for a tool description to carry. Stated as what a
    /// search SKIPS, so a reader told "absent" knows where it was not looked for.</summary>
    public const string Summary = $"these directories ({ExcludedDirsText}), the uploaded reference "
        + $"websites under {ReferenceDirectory.Path} unless the search starts inside it, and any file over 1 MB";

    /// <summary>The repository root a step resolves against: its working directory, or /work.</summary>
    public static string RepoRootOf(Step step)
    {
        ArgumentNullException.ThrowIfNull(step);
        return string.IsNullOrEmpty(step.WorkingDirectory) ? "/work" : step.WorkingDirectory;
    }

    /// <summary>
    /// 2026-10-01-283df: the ROOTED exclusion. Unlike the list above, which is skipped wherever it
    /// appears, the reference directory is skipped only at the repository root and only by a search
    /// that starts OUTSIDE it — a search rooted inside it is a search that asked for it.
    /// </summary>
    public static bool SkipsReferences(string searchRoot, string repoRoot) =>
        !IsWithin(searchRoot, ReferenceRoot(repoRoot));

    /// <summary>True when a full path lies inside the repository's reference directory.</summary>
    public static bool IsUnderReferences(string fullPath, string repoRoot) =>
        IsWithin(fullPath, ReferenceRoot(repoRoot));

    private static string ReferenceRoot(string repoRoot) =>
        Normal(System.IO.Path.Combine(repoRoot, ReferenceDirectory.Path));

    private static bool IsWithin(string path, string directory)
    {
        var full = Normal(path);
        return full == directory || full.StartsWith(directory + System.IO.Path.DirectorySeparatorChar, StringComparison.Ordinal);
    }

    private static string Normal(string path) =>
        System.IO.Path.GetFullPath(path).TrimEnd(System.IO.Path.DirectorySeparatorChar);

    /// <summary>True when a full path sits under one of the excluded directories.</summary>
    public static bool IsExcluded(string fullPath)
    {
        ArgumentNullException.ThrowIfNull(fullPath);
        var sep = System.IO.Path.DirectorySeparatorChar;
        foreach (var dir in ExcludedDirs)
            if (fullPath.Contains($"{sep}{dir}{sep}", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
